# Database Calls Analysis & Scalability Report

> **Generated:** 2026-03-31  
> **Scope:** Full solution analysis for repeated/infinite database calls and public scalability  
> **Constraint:** Preserve current real-time UX responsiveness

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Infinite/Repeated Database Call Analysis](#2-infiniterepeated-database-call-analysis)
3. [Database Load Reduction Opportunities](#3-database-load-reduction-opportunities)
4. [Architectural Improvements for Scalability](#4-architectural-improvements-for-scalability)
5. [Prioritized Action Plan](#5-prioritized-action-plan)
6. [Instrumentation and Verification](#6-instrumentation-and-verification)
7. [Most Likely Root Cause](#7-most-likely-root-cause)
8. [Best Target Architecture](#8-best-target-architecture)
9. [How to Preserve UX While Scaling](#9-how-to-preserve-ux-while-scaling)
10. [First 10 Changes I Would Make](#10-first-10-changes-i-would-make)

---

## 1. Executive Summary

### Top 5 Most Likely Causes of Excessive Database Load

1. **FusionCache configured with a 2ms TTL (effectively disabled caching)**  
   `src/CardGames.Poker.Api/Program.cs:145` sets `.WithDefaultEntryOptions(options => options.Duration = TimeSpan.FromMilliseconds(2))`. This means every `HybridCache.GetOrCreateAsync` call falls through to the database within 2ms — effectively on every request. The TODO comment confirms the intended value was 5 minutes. Every cached query handler (GetGame, GetGamePlayers, GetCurrentDrawPlayer, GetCurrentBettingRound, GetAvailablePokerGames, etc.) hits SQL Server on every call.

2. **BroadcastGameStateAsync triggers N+1 database round-trips per broadcast**  
   `GameStateBroadcaster.BroadcastGameStateAsync` (line 53) calls `BuildPublicStateAsync` (1 full game query), then `GetPlayerUserIdsAsync` (1 query), then `SendPrivateStateToUserAsync` per player (N queries — one `BuildPrivateStateAsync` per player). For a 6-player table, each broadcast = **8+ separate database queries**. This is called after every game-state-changing command via `GameStateBroadcastingBehavior`.

3. **ContinuousPlayBackgroundService polls every 1 second with multiple queries per tick**  
   `ContinuousPlayBackgroundService.ExecuteAsync` runs `ProcessGamesReadyForNextHandAsync` in a `while` loop with `Task.Delay(_checkInterval)` at 1 second. Each tick runs: `ProcessAbandonedGamesAsync`, `ProcessDrawCompleteGamesAsync`, `ProcessKlondikeRevealGamesAsync`, `ProcessInBetweenResolutionGamesAsync`, and a query for `gamesReadyForNextHand` — that's **5+ database queries per second** even when zero games are active. Each game found triggers further queries, `SaveChangesAsync`, and `BroadcastGameStateAsync` (which adds 8+ more queries per game).

4. **GameHub.JoinGame triggers a full state snapshot build from the database**  
   `GameHub.JoinGame` (line 37) calls `SendStateSnapshotToCallerAsync`, which calls both `BuildPublicStateAsync` and `BuildPrivateStateAsync` — 2+ full database queries per connection. Combined with SignalR reconnection behavior, any network hiccup causes clients to re-join and trigger fresh database loads.

5. **TableStateBuilder.BuildPublicStateAsync executes multiple separate queries instead of one**  
   `BuildPublicStateAsync` (line 87) runs: (a) a Games query with includes for GameType and Pots, (b) a separate GamePlayers query with Player and Cards includes, (c) a separate user lookup, (d) conditional hand history queries, (e) conditional community card queries, (f) pot calculation queries. These are independent queries that could be consolidated.

### Top 5 Most Important Scalability Risks for Public Launch

1. **SQL Server is the real-time state engine**: Active game state (current phase, player positions, chip stacks, betting rounds, cards) lives entirely in SQL Server. Every player action reads and writes the full game state from/to SQL. At 100 concurrent tables with 6 players each, every bet/fold/check triggers 10+ queries.

2. **No in-memory game state layer**: There is no `ConcurrentDictionary<Guid, GameState>` or Redis-backed game state. Every game action is: load full entity graph → modify → save → reload for broadcast. The architecture treats SQL Server as a real-time message bus.

3. **Broadcast amplification**: Each game action triggers a full-table broadcast that rebuilds the complete public state + per-player private state from the database. With 6 players, one bet = 1 command + 8 queries for broadcast = ~10 DB round-trips minimum. During a busy betting round, this repeats for every player action.

4. **Background service polling is not adaptive**: `ContinuousPlayBackgroundService` runs the same query set every second regardless of active game count. With 1,000 idle games and 10 active ones, all 1,010 games are queried.

5. **No query result caching for unchanged state**: After a player action, the command handler saves changes, then the broadcast pipeline immediately queries the same data back from the database (the 2ms cache does not help). There is no mechanism to pass the already-loaded state forward to the broadcaster.

### Top Risks to Preserving the Current Live Interactive UX While Scaling

1. **Naive caching could show stale cards/bets**: Poker requires absolute data consistency for cards and chip amounts. Any caching of game state must be invalidated on every mutation.
2. **Reducing broadcast frequency degrades perceived responsiveness**: Players expect to see bet/fold/raise results instantly. Batching or throttling broadcasts would make the game feel laggy.
3. **Moving to eventual persistence could risk data loss on crashes**: If game state is in memory and the server crashes, the current hand state is lost. Recovery mechanisms are needed.
4. **Horizontal scaling requires shared state**: Moving game state to memory works for a single server but fails with multiple instances unless backed by Redis or sticky sessions.

---

## 2. Infinite/Repeated Database Call Analysis

### Issue 2.1: FusionCache 2ms TTL Effectively Disables All Caching

**Severity: Critical**

**Files involved:**
- `src/CardGames.Poker.Api/Program.cs` (line 145)
- All query handlers using `HybridCache.GetOrCreateAsync`:
  - `Features/Games/Common/v1/Queries/GetCurrentDrawPlayer/GetCurrentDrawPlayerQueryHandler.cs`
  - `Features/Games/Common/v1/Queries/GetGame/GetGameQueryHandler.cs`
  - `Features/Games/Common/v1/Queries/GetGamePlayers/GetGamePlayersQueryHandler.cs`
  - `Features/Games/Common/v1/Queries/GetCurrentBettingRound/GetCurrentBettingRoundQueryHandler.cs`
  - `Features/Games/Common/v1/Queries/GetGames/GetGamesQueryHandler.cs`
  - `Features/Games/Common/v1/Queries/GetAvailablePokerGames/GetAvailablePokerGamesQueryHandler.cs`
  - `Features/Games/Baseball/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs`
  - `Features/Games/PairPressure/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs`

**Code pattern:**
```csharp
// Program.cs:138-147
builder.Services.AddFusionCache()
    .WithSerializer(...)
    //TODO:ROB = .WithDefaultEntryOptions(options => options.Duration = TimeSpan.FromMinutes(5))
    .WithDefaultEntryOptions(options => options.Duration = TimeSpan.FromMilliseconds(2))
    .WithRegisteredDistributedCache()
    .AsHybridCache();
```

**Call chain:**
1. Any MediatR query handler calls `hybridCache.GetOrCreateAsync(cacheKey, factory)`
2. FusionCache checks L1 (in-memory) → expired in 2ms → miss
3. FusionCache checks L2 (Redis) → expired in 2ms → miss
4. Falls through to factory function → hits SQL Server
5. Result cached for 2ms → next request misses again

**Why it repeats:** 2ms is shorter than a single HTTP round-trip. By the time the response reaches the client, the cache entry is already expired. This means the cache is never serving a hit. Every query goes to SQL Server every time.

**Fix:**
```csharp
// Program.cs — change the default TTL and use per-query overrides
builder.Services.AddFusionCache()
    .WithSerializer(new FusionCacheSystemTextJsonSerializer(new System.Text.Json.JsonSerializerOptions
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    }))
    .WithDefaultEntryOptions(options => options.Duration = TimeSpan.FromMinutes(5))
    .WithRegisteredDistributedCache()
    .AsHybridCache();
```

For game-state queries that must be fresh, use per-query short durations or explicit cache invalidation after mutations rather than a globally-disabled cache:
```csharp
// In query handlers for volatile game state
return await hybridCache.GetOrCreateAsync(
    cacheKey,
    async _ => { /* DB query */ },
    new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(2) },
    cancellationToken: cancellationToken
);

// In mutation handlers, invalidate after SaveChangesAsync
await hybridCache.RemoveAsync($"game:{gameId}");
```

**UX impact:** None negative — this change reduces database load without any UX degradation. Static data (game types, rules, available games) will be served from cache. Game state queries should use short-but-meaningful TTLs (1-5 seconds) with explicit invalidation on mutations.

---

### Issue 2.2: BroadcastGameStateAsync N+1 Pattern

**Severity: Critical**

**Files involved:**
- `src/CardGames.Poker.Api/Services/GameStateBroadcaster.cs` (lines 53-91)
- `src/CardGames.Poker.Api/Services/TableStateBuilder.cs` (BuildPublicStateAsync, BuildPrivateStateAsync, GetPlayerUserIdsAsync)
- `src/CardGames.Poker.Api/Infrastructure/PipelineBehaviors/GameStateBroadcastingBehavior.cs` (line 83)

**Code pattern:**
```csharp
// GameStateBroadcaster.cs:53-91
public async Task BroadcastGameStateAsync(Guid gameId, CancellationToken cancellationToken)
{
    // Query 1: Full public state (Game + GameType + Pots + GamePlayers + Cards)
    var publicState = await _tableStateBuilder.BuildPublicStateAsync(gameId, cancellationToken);
    
    await _hubContext.Clients.Group(groupName).SendAsync("TableStateUpdated", publicState, ...);
    
    // Query 2: Get all player user IDs
    var playerUserIds = await _tableStateBuilder.GetPlayerUserIdsAsync(gameId, cancellationToken);
    
    // Queries 3..N+2: One private state query PER player
    foreach (var userId in playerUserIds)
    {
        await SendPrivateStateToUserAsync(gameId, userId, cancellationToken);
        // Each call runs BuildPrivateStateAsync → another DB query
    }
}
```

**Call chain:**
1. Player places a bet → `ProcessBettingActionCommand` handler runs
2. Handler saves changes to DB
3. `GameStateBroadcastingBehavior` auto-fires `BroadcastGameStateAsync`
4. `BuildPublicStateAsync` → 2+ DB queries (games, gameplayers)
5. `GetPlayerUserIdsAsync` → 1 DB query
6. For each of N players, `BuildPrivateStateAsync` → 1 DB query each

**Why it's excessive:** For a 6-player table, each player action triggers **8+ database queries** just for the broadcast. During an active betting round with rapid actions, this compounds.

**Fix:** Batch all private state into a single query and distribute from memory:
```csharp
public async Task BroadcastGameStateAsync(Guid gameId, CancellationToken ct)
{
    // Single query: load game + gameplayers + cards + pots in one round-trip
    var (publicState, privateStates) = await _tableStateBuilder.BuildFullStateAsync(gameId, ct);
    
    if (publicState is not null)
    {
        await _hubContext.Clients.Group(groupName).SendAsync("TableStateUpdated", publicState, ct);
        ManageActionTimer(gameId, publicState);
    }
    
    // Send each player's private state from already-loaded data
    foreach (var (userId, privateState) in privateStates)
    {
        await _hubContext.Clients.User(userId).SendAsync("PrivateStateUpdated", privateState, ct);
    }
}
```

**UX impact:** No degradation — clients receive the same data, just built more efficiently server-side.

---

### Issue 2.3: ContinuousPlayBackgroundService 1-Second Polling with Unconditional Queries

**Severity: High**

**Files involved:**
- `src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs` (lines 58-130)

**Code pattern:**
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await ProcessGamesReadyForNextHandAsync(stoppingToken);  // 5+ queries every tick
        await Task.Delay(_checkInterval, stoppingToken);          // 1 second
    }
}

internal async Task ProcessGamesReadyForNextHandAsync(CancellationToken cancellationToken)
{
    await ProcessAbandonedGamesAsync(...);           // Query 1
    await ProcessDrawCompleteGamesAsync(...);        // Query 2
    await ProcessKlondikeRevealGamesAsync(...);      // Query 3
    await ProcessInBetweenResolutionGamesAsync(...); // Query 4
    var gamesReadyForNextHand = await context.Games  // Query 5
        .Where(g => ...)
        .Include(g => g.GamePlayers)
        .Include(g => g.GameType)
        .ToListAsync(cancellationToken);
    
    foreach (var game in gamesReadyForNextHand)
    {
        await StartNextHandAsync(...);  // Multiple queries + SaveChanges + BroadcastGameState
    }
}
```

**Why it repeats excessively:** 5+ database queries fire every single second regardless of whether any games are active. With 100 active tables, the `StartNextHandAsync` method can itself trigger 10+ queries per game, plus a full broadcast (8+ queries per game). This means peak load could be hundreds of queries per second from this single service.

**Fix:**
```csharp
// Option A: Combine into a single query per tick
internal async Task ProcessGamesReadyForNextHandAsync(CancellationToken cancellationToken)
{
    var now = DateTimeOffset.UtcNow;
    
    // Single query to find ALL games that need attention
    var games = await context.Games
        .Where(g => g.Status == GameStatus.InProgress || g.Status == GameStatus.BetweenHands)
        .Where(g => 
            // Abandoned
            (inProgressPhases.Contains(g.CurrentPhase) && !g.GamePlayers.Any(gp => gp.Status == GamePlayerStatus.Active)) ||
            // Draw complete
            (g.CurrentPhase == nameof(Phases.DrawComplete) && g.DrawCompletedAt != null && g.DrawCompletedAt.Value.AddSeconds(3) <= now) ||
            // Ready for next hand
            (readyPhases.Contains(g.CurrentPhase) && g.NextHandStartsAt != null && g.NextHandStartsAt <= now)
            // etc.
        )
        .Include(g => g.GamePlayers)
        .Include(g => g.GameType)
        .ToListAsync(cancellationToken);
    
    foreach (var game in games)
    {
        // Route to appropriate handler
    }
}

// Option B: Use event-driven approach instead of polling
// When a hand completes, schedule a delayed task via Timer or Hangfire
```

**UX impact:** No degradation — the same transitions happen at the same time. The only change is reducing redundant database queries when no games need processing.

---

### Issue 2.4: GameHub.JoinGame Full State Rebuild on Every Connection

**Severity: High**

**Files involved:**
- `src/CardGames.Poker.Api/Hubs/GameHub.cs` (lines 37-60, 108-158)
- `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`

**Code pattern:**
```csharp
// GameHub.cs
public async Task JoinGame(Guid gameId)
{
    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    await SendStateSnapshotToCallerAsync(gameId);  // Full DB load
}

private async Task SendStateSnapshotToCallerAsync(Guid gameId)
{
    var publicState = await _tableStateBuilder.BuildPublicStateAsync(gameId, ...);   // DB query
    var privateState = await _tableStateBuilder.BuildPrivateStateAsync(gameId, userId, ...); // DB query
    
    await Clients.Caller.SendAsync("TableStateUpdated", publicState, ...);
    await Clients.Caller.SendAsync("PrivateStateUpdated", privateState, ...);
}
```

**Why it can repeat:** SignalR clients automatically reconnect on network issues. Each reconnection calls `JoinGame` again, triggering 2+ database queries. On spotty mobile connections, this can happen repeatedly. If multiple clients reconnect simultaneously (e.g., after a server restart), this creates a query storm.

**Fix:** 
- Cache the last-broadcast public state per game in memory (it was just built for the broadcast)
- Serve reconnecting clients from this cache rather than rebuilding from DB
- Only fall through to DB if no cached state exists

```csharp
// Use a ConcurrentDictionary<Guid, TableStatePublicDto> as a write-through cache
private async Task SendStateSnapshotToCallerAsync(Guid gameId)
{
    var publicState = _stateCache.GetOrDefault(gameId) 
        ?? await _tableStateBuilder.BuildPublicStateAsync(gameId);
    
    // Private state still needs per-user query, but can be optimized similarly
    var privateState = await _tableStateBuilder.BuildPrivateStateAsync(gameId, userId);
    
    await Clients.Caller.SendAsync("TableStateUpdated", publicState);
    await Clients.Caller.SendAsync("PrivateStateUpdated", privateState);
}
```

**UX impact:** None negative — the cached state is the same data that was just broadcast to all clients. Reconnecting clients get the same state faster.

---

### Issue 2.5: Multiple SaveChangesAsync Calls in Single Game Flow Operations

**Severity: Medium**

**Files involved:**
- `src/CardGames.Poker.Api/GameFlow/InBetweenFlowHandler.cs` (7 SaveChangesAsync calls)
- `src/CardGames.Poker.Api/GameFlow/ScrewYourNeighborFlowHandler.cs` (5 SaveChangesAsync calls)
- `src/CardGames.Poker.Api/GameFlow/BaseballFlowHandler.cs` (2 SaveChangesAsync calls)
- `src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs` (multiple per game transition)

**Code pattern (InBetweenFlowHandler):**
```csharp
// Multiple SaveChangesAsync within a single game action
await context.SaveChangesAsync(cancellationToken);  // Save deal state
// ... more logic ...
await context.SaveChangesAsync(cancellationToken);  // Save betting round
// ... more logic ...  
await context.SaveChangesAsync(cancellationToken);  // Save phase change
```

**Why it's excessive:** Each `SaveChangesAsync` is a separate database round-trip. The `InBetweenFlowHandler` has 7 calls within a single player action flow — that's 7 separate DB transactions when one would suffice in most cases.

**Fix:** Batch changes and call `SaveChangesAsync` once at the end of the operation:
```csharp
// Accumulate all changes, then save once
player.ChipStack -= betAmount;
bettingRound.CurrentBet = betAmount;
game.CurrentPhase = nextPhase;
context.BettingActionRecords.Add(actionRecord);
// ... all modifications ...

await context.SaveChangesAsync(cancellationToken);  // Single round-trip
```

Where intermediate saves are needed for concurrency (e.g., generating IDs), use `context.Database.BeginTransactionAsync()` with a single commit.

**UX impact:** Slightly improved responsiveness — fewer round-trips means faster action processing.

---

### Issue 2.6: ActionTimerService 1-Second SignalR Ticks Triggering State Lookups

**Severity: Medium**

**Files involved:**
- `src/CardGames.Poker.Api/Services/ActionTimerService.cs` (lines 36, 139, 193)

**Code pattern:**
```csharp
// Timer ticks every 1 second for each active game
private void OnTimerTickAsync(...)
{
    // Broadcasts timer state to all clients every second
    BroadcastTimerStateAsync(gameId, timerState);
}
```

**Why it matters:** While the timer itself uses in-memory `ConcurrentDictionary<Guid, GameTimerState>` (good), each tick broadcasts to all clients via SignalR. With 100 active tables, that's 100 SignalR group messages per second just for timers. This doesn't directly cause DB queries, but adds SignalR load. Additionally, when the timer expires, `onExpired` calls `_autoActionService.PerformAutoActionAsync` which triggers a full game action cycle (DB read → modify → save → broadcast → 8+ queries).

**Fix:** 
- Send the timer start/duration once, let clients countdown locally (they already do this)
- Only send a SignalR message when the timer expires, not every second
- Keep the 1-second broadcast only as a sync mechanism, not as the primary timer

**UX impact:** Minimal — clients already run local timers. Removing per-second broadcasts would save bandwidth but the local timer already provides the countdown UI.

---

### Issue 2.7: BuildPublicStateAsync Runs Multiple Sequential Queries

**Severity: Medium**

**Files involved:**
- `src/CardGames.Poker.Api/Services/TableStateBuilder.cs` (lines 87-120+)

**Code pattern:**
```csharp
public async Task<TableStatePublicDto?> BuildPublicStateAsync(Guid gameId, CancellationToken ct)
{
    // Query 1: Game + GameType + Pots
    var game = await _context.Games
        .Include(g => g.GameType)
        .Include(g => g.Pots)
        .AsNoTracking()
        .FirstOrDefaultAsync(g => g.Id == gameId, ct);
    
    // Query 2: GamePlayers + Player + Cards
    var gamePlayers = await _context.GamePlayers
        .Where(gp => gp.GameId == gameId && gp.Status != GamePlayerStatus.Left)
        .Include(gp => gp.Player)
        .Include(gp => gp.Cards)
        .OrderBy(gp => gp.SeatPosition)
        .AsNoTracking()
        .ToListAsync(ct);
    
    // Additional queries for community cards, hand history, pot calculations...
}
```

**Why it's excessive:** Two separate queries where one would work. The game, its players, pots, and cards could all be loaded in a single query with proper includes.

**Fix:**
```csharp
var game = await _context.Games
    .Include(g => g.GameType)
    .Include(g => g.Pots)
    .Include(g => g.GamePlayers.Where(gp => gp.Status != GamePlayerStatus.Left))
        .ThenInclude(gp => gp.Player)
    .Include(g => g.GamePlayers.Where(gp => gp.Status != GamePlayerStatus.Left))
        .ThenInclude(gp => gp.Cards)
    .AsSplitQuery()  // Use split query to avoid cartesian explosion
    .AsNoTracking()
    .FirstOrDefaultAsync(g => g.Id == gameId, ct);
```

**UX impact:** Faster state building = faster broadcasts = same or better UX.

---

### Issue 2.8: Hand Settlement Creates Multiple Independent SaveChangesAsync Calls

**Severity: Medium**

**Files involved:**
- `src/CardGames.Poker.Api/Services/HandSettlementService.cs`
- `src/CardGames.Poker.Api/Services/PlayerChipWalletService.cs`
- `src/CardGames.Poker.Api/Services/HandHistoryRecorder.cs`

**Code pattern:**
```
PerformShowdown
  → HandSettlementService.SettleHandAsync()
    → For each winner: PlayerChipWalletService.RecordHandSettlementAsync()
      → SaveChangesAsync() per player  // N saves for N winners
  → HandHistoryRecorder.RecordHandHistoryAsync()
    → SaveChangesAsync()                // 1 more save
```

**Why it's excessive:** A 3-way pot split causes 4 separate `SaveChangesAsync` calls. These should be batched into a single transaction.

**Fix:** Pass the `CardsDbContext` through and call `SaveChangesAsync` once after all settlement logic completes.

**UX impact:** Faster showdown resolution.

---

## 3. Database Load Reduction Opportunities

### 3.1 Repeated Reads That Can Be Cached

| Query | Current Behavior | Recommended Cache Duration | Impact |
|-------|-----------------|---------------------------|--------|
| `GetAvailablePokerGames` | 2ms cache (no cache) | 1 hour (static data) | High — called on every Lobby load |
| `GetGame` (game metadata) | 2ms cache | 30 seconds with invalidation on mutation | High |
| `GetGamePlayers` | 2ms cache | 5 seconds with invalidation on join/leave | Medium |
| `GetCurrentBettingRound` | 2ms cache | 2 seconds with invalidation on bet | Medium |
| Game rules/metadata | Cached client-side 1 hour (good) | Keep current | Low |
| User profiles (avatars, names) | Loaded per broadcast in BuildPublicState | 5 minutes | Medium |

### 3.2 Queries That Should Use Projection

| Query | Current Issue | Fix |
|-------|--------------|-----|
| `BuildPublicStateAsync` loads full Game entity | Only needs phase, hand number, dealer position | Use `.Select()` projection |
| `GetPlayerUserIdsAsync` | Loads full GamePlayer entities for user IDs | Use `.Select(gp => gp.Player.UserId)` |
| `ProcessAbandonedGamesAsync` | Loads full Game entities with includes | Only needs game ID and player count |
| Background service game queries | Load full entity graphs with all includes | Select only fields needed for transition logic |

### 3.3 Queries That Should Use AsNoTracking

Most read queries already use `AsNoTracking()` — this is well-implemented. Exceptions:

| Query | File | Fix |
|-------|------|-----|
| `ContinuousPlayBackgroundService` game queries | Lines 108-117 | Add `.AsNoTracking()` for the initial check, load tracked only for games that need modification |

### 3.4 Writes That Can Be Batched or Delayed

| Write | Current Behavior | Recommendation |
|-------|-----------------|----------------|
| `BettingActionRecord` creation | Written with each bet | Keep immediate (audit trail) |
| `HandHistory` recording | Written at showdown | Can be delayed to background queue |
| `PlayerChipLedgerEntry` | Written per settlement | Batch all entries in single SaveChanges |
| `PotContribution` tracking | Written per ante/bet | Batch per betting round |
| Background service phase transitions | Multiple SaveChanges per game | Single SaveChanges per game |

### 3.5 Places Where the UI Is Reloading Too Much Data

| Component | Pattern | Fix |
|-----------|---------|-----|
| `TablePlay.razor` receives full `TableStatePublicDto` on every action | Contains all seats, all cards, all pots, hand history | Send delta updates for common changes (pot amount, current player, phase) |
| `Lobby.razor` calls `GetActiveGamesAsync` in `OnInitializedAsync` | Loads all active games | Already good — could paginate if game count grows large |
| Full state rebuild on every BroadcastGameStateAsync | Every action rebuilds entire table state | Cache last-known state, compute and send only the delta |

### 3.6 Opportunities to Push Deltas Instead of Full Refreshes

| Event | Current: Full State Sent | Better: Delta Sent |
|-------|------------------------|-------------------|
| Player bets | Full `TableStatePublicDto` + per-player `PrivateStateDto` | `{ potTotal: X, playerChips: Y, currentBet: Z, nextPlayer: N }` |
| Player folds | Full state | `{ playerFolded: seatIndex, nextPlayer: N }` |
| Phase change (e.g., flop dealt) | Full state | `{ phase: "Flop", communityCards: [...], potTotal: X }` |
| Timer tick | `ActionTimerStateDto` every second | Send once with duration, client counts locally |

---

## 4. Architectural Improvements for Scalability

### 4.1 What State Should Remain in SQL Server

| Data | Persist Immediately | Persist Eventually | Keep in Memory Only |
|------|--------------------|--------------------|-------------------|
| User accounts/profiles | ✅ | | |
| Chip account balances | ✅ | | |
| Hand history (audit) | | ✅ (after hand completes) | |
| League standings | ✅ | | |
| Game configuration (ante, blinds, variant) | ✅ | | |
| **Active hand state** | | | ✅ (checkpoint on phase change) |
| **Current betting round** | | | ✅ |
| **Player card assignments** | | | ✅ |
| **Pot amounts during hand** | | | ✅ |
| **Player turn/action state** | | | ✅ |

### 4.2 What State Should Move to Memory or Redis

**In-Memory (per-server, for single-instance or with sticky sessions):**
- Active game state per table (`ConcurrentDictionary<Guid, GameState>`)
- Current hand state (cards, bets, pots, phase, turn)
- Action timer state (already in memory — good)
- Last-broadcast state cache (for reconnecting clients)

**Redis (for multi-instance deployment):**
- Active game state (when horizontally scaled)
- Player session mapping (which server owns which game)
- Pub/Sub for cross-instance SignalR (already using Redis distributed cache)
- Rate limiting counters

### 4.3 How Active Table/Game State Should Be Managed

**Target architecture: In-Memory Game Engine with Database Checkpointing**

```
┌──────────────┐     ┌──────────────────┐     ┌────────────┐
│  Blazor Web  │────▶│  ASP.NET API     │────▶│  SQL Server │
│  (SignalR)   │◀────│  + Game Engine    │     │  (durable)  │
└──────────────┘     │  (in-memory)      │     └────────────┘
                     │                    │         ▲
                     │  GameStateManager  │─────────┘
                     │  - Active hands    │  checkpoint on:
                     │  - Current bets    │  - hand complete
                     │  - Card state      │  - player join/leave
                     │  - Phase tracking  │  - significant state change
                     └──────────────────┘
```

**Key principle:** Read from memory, write to DB on meaningful checkpoints.

- **On player action (bet/fold/raise):** Update in-memory state → broadcast via SignalR → no DB write
- **On hand completion:** Flush hand result to DB (history, chip settlements, pot distribution)
- **On player join/leave:** Update DB (seat assignment, player status)
- **On game create/delete:** Full DB write
- **On phase transition:** Optional checkpoint for crash recovery

### 4.4 How Clients Should Receive Updates

**Current:** Full state push on every change  
**Target:** Tiered update strategy

| Update Type | Frequency | Content | Delivery |
|-------------|-----------|---------|----------|
| Full state snapshot | On connect/reconnect only | Complete `TableStatePublicDto` + `PrivateStateDto` | SignalR to individual client |
| Game action delta | Per action | `{ action, seatIndex, amount, potTotal, nextPlayer }` | SignalR to game group |
| Phase transition | Per phase change | `{ phase, communityCards, dealerIndex }` | SignalR to game group |
| Private card update | On deal/draw | `{ hand: [...] }` | SignalR to individual player |
| Timer state | On start/expire only | `{ duration, startedAt }` | SignalR to game group |

### 4.5 How to Reduce Database Writes During Active Play

1. **Defer `BettingActionRecord` writes** — accumulate in memory, flush at hand completion as a batch
2. **Defer `PotContribution` writes** — calculate in memory, persist only final pot state
3. **Defer `GameCard` updates** — track dealt/discarded cards in memory during the hand
4. **Immediate writes only for:** game creation, player join/leave, hand settlement (chip accounts), hand history

### 4.6 How to Handle Reconnect/Recovery

1. **Client reconnect:** Serve last-known state from in-memory cache (already broadcast, so it's fresh)
2. **Server restart:** Load active games from DB checkpoints, resume from last-known phase
3. **In-memory state loss:** Each phase transition writes a checkpoint to DB; on recovery, replay from last checkpoint

### 4.7 How to Scale Horizontally

**Phase 1 (Single instance — current):**
- Move game state to in-memory `ConcurrentDictionary`
- Use SignalR with in-process groups (already working)

**Phase 2 (Multiple instances):**
- Add Redis backplane for SignalR: `builder.Services.AddSignalR().AddStackExchangeRedis()`
- Store game state in Redis instead of in-process memory
- Use Redis pub/sub for cross-instance game state coordination
- Use sticky sessions for SignalR connections (WebSocket affinity)

**Phase 3 (High scale):**
- Shard games across instances (game ID → instance mapping)
- Use dedicated game-server processes per N tables
- Consider Orleans or Akka.NET for virtual actor model (one actor per table)

### 4.8 Redis Backplane / Distributed Cache / Pub-Sub

**Already configured but underutilized:**
- `builder.AddRedisDistributedCache("cache")` — configured in `Program.cs:126`
- FusionCache is connected to Redis L2 — but 2ms TTL negates it
- Redis backplane for SignalR is **not** configured yet

**Recommended additions:**
```csharp
// SignalR Redis backplane (for horizontal scaling)
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("CardGames");
    });

// Redis for game state (when scaling horizontally)
builder.Services.AddSingleton<IGameStateStore, RedisGameStateStore>();
```

### 4.9 How to Avoid Race Conditions and Duplicate Actions

**Current protections (good):**
- `EntityWithRowVersion.RowVersion` (byte[] RowVersion) for optimistic concurrency on Game entity
- `DbUpdateConcurrencyException` handling in flow handlers
- `DbUpdateException` with unique constraint check in HandHistoryRecorder (idempotency)

**Additional recommendations:**
- Add `SemaphoreSlim` per game ID for in-memory state mutations
- Use `context.Database.BeginTransactionAsync()` for multi-step operations
- Add idempotency keys to player action commands (prevent duplicate bet submissions)

### 4.10 Preserving Responsiveness While Scaling

| Change | DB Load Reduction | Latency Impact | UX Impact |
|--------|-------------------|----------------|-----------|
| Fix FusionCache TTL | High (static queries cached) | Improved | None — same data, faster |
| Batch broadcast queries | High (8→2 queries per broadcast) | Improved | None — same data, faster |
| In-memory game state | Very High (eliminate per-action DB reads) | Much improved | None — same data, much faster |
| Delta updates | Medium (less data to serialize) | Improved | Slightly different client code, same perceived result |
| Background service optimization | Medium (fewer idle queries) | None | None |
| Hand history → background queue | Low (defer non-critical write) | Slightly improved | None — history still appears, just written async |

---

## 5. Prioritized Action Plan

### Quick Wins (1-2 hours each)

| # | Change | Files | DB Load Impact | UX Impact |
|---|--------|-------|----------------|-----------|
| 1 | **Fix FusionCache TTL from 2ms to 5 minutes** | `Program.cs:145` | -40% read queries for static data | None (improved) |
| 2 | **Add AsNoTracking to background service initial queries** | `ContinuousPlayBackgroundService.cs` | -5% tracking overhead | None |
| 3 | **Use projection for GetPlayerUserIdsAsync** | `TableStateBuilder.cs` | Minor improvement | None |
| 4 | **Reduce ActionTimer broadcasts to start/expire only** | `ActionTimerService.cs` | No DB impact, -50% SignalR messages | None (clients already have local timer) |

### Medium Refactors (1-2 days each)

| # | Change | Files | DB Load Impact | UX Impact |
|---|--------|-------|----------------|-----------|
| 5 | **Consolidate BuildPublicStateAsync into single query** | `TableStateBuilder.cs` | -30% queries per broadcast | Faster state delivery |
| 6 | **Batch all private states in BroadcastGameStateAsync** | `GameStateBroadcaster.cs`, `TableStateBuilder.cs` | -50% queries per broadcast | Faster state delivery |
| 7 | **Combine background service queries into single query** | `ContinuousPlayBackgroundService.cs` | -80% background queries | None |
| 8 | **Batch SaveChangesAsync in flow handlers** | All FlowHandler files | -60% write round-trips in complex flows | Faster action processing |
| 9 | **Cache last-broadcast state for reconnect** | `GameHub.cs`, `GameStateBroadcaster.cs` | -90% reconnect queries | Faster reconnect |
| 10 | **Add explicit cache invalidation on mutations** | All command handlers | Enables meaningful cache TTLs | None |

### Larger Architectural Changes (1-2 weeks each)

| # | Change | Files | DB Load Impact | UX Impact |
|---|--------|-------|----------------|-----------|
| 11 | **In-memory GameStateManager** for active hands | New service, refactor flow handlers | -90% game-action DB reads/writes | Much faster actions |
| 12 | **Delta-based SignalR updates** instead of full state | `GameStateBroadcaster.cs`, Blazor components | -70% SignalR payload | Same UX, less bandwidth |
| 13 | **Background queue for hand history writes** | `HandHistoryRecorder.cs`, new background service | -20% synchronous DB writes | None |
| 14 | **Redis backplane for SignalR** | `Program.cs`, infrastructure | Enables horizontal scaling | None |
| 15 | **Game state in Redis** for multi-instance | New `RedisGameStateStore` | Enables horizontal scaling | None |

### Expected Combined Impact

| Metric | Current (estimated) | After Quick Wins | After Medium Refactors | After Architecture Changes |
|--------|--------------------:|------------------:|------------------------:|----------------------------:|
| DB queries per player action | ~15 | ~10 | ~4 | ~1 (checkpoint only) |
| DB queries per second (idle, 10 tables) | ~50 | ~30 | ~5 | ~1 |
| DB queries per second (active, 10 tables) | ~150-300 | ~100 | ~40 | ~10 |
| SignalR messages per second (10 tables) | ~100+ | ~60 | ~40 | ~30 |
| Avg action-to-display latency | ~200ms | ~150ms | ~80ms | ~30ms |

---

## 6. Instrumentation and Verification

### 6.1 EF Core Query Logging

Add to `Program.cs` (development only):
```csharp
builder.Services.AddDbContext<CardsDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    
    // Enable detailed query logging in development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
        options.LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Command.Name }, LogLevel.Information);
    }
});
```

### 6.2 Query Counters

Add a MediatR pipeline behavior that counts DB queries per request:
```csharp
public sealed class QueryCountingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly DiagnosticListener _listener;
    
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var queryCount = 0;
        // Subscribe to EF Core diagnostic events
        using var subscription = DiagnosticListener.AllListeners.Subscribe(listener =>
        {
            if (listener.Name == "Microsoft.EntityFrameworkCore")
            {
                listener.Subscribe(evt =>
                {
                    if (evt.Key == "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted")
                        Interlocked.Increment(ref queryCount);
                });
            }
        });
        
        var result = await next();
        
        if (queryCount > 5) // Flag requests with high query counts
        {
            _logger.LogWarning("{RequestType} executed {QueryCount} DB queries", typeof(TRequest).Name, queryCount);
        }
        
        return result;
    }
}
```

### 6.3 Timing Metrics

Already partially configured with OpenTelemetry. Add custom metrics:
```csharp
// In Program.cs, add custom meters
var meter = new Meter("CardGames.Poker.Api");
var broadcastDuration = meter.CreateHistogram<double>("game.broadcast.duration_ms");
var broadcastQueryCount = meter.CreateHistogram<int>("game.broadcast.query_count");
var actionProcessingDuration = meter.CreateHistogram<double>("game.action.processing_ms");
var backgroundServiceDuration = meter.CreateHistogram<double>("game.background.tick_ms");
```

### 6.4 SignalR Connection/Message Metrics

```csharp
// Custom SignalR hub filter for metrics
public sealed class SignalRMetricsFilter : IHubFilter
{
    private static readonly Counter<int> HubMethodCalls = 
        new Meter("CardGames.SignalR").CreateCounter<int>("signalr.hub.method_calls");
    private static readonly Counter<int> ConnectionCount = 
        new Meter("CardGames.SignalR").CreateCounter<int>("signalr.connections");
    
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        HubMethodCalls.Add(1, new KeyValuePair<string, object?>("method", context.HubMethodName));
        return await next(context);
    }
}

// Register in Program.cs
builder.Services.AddSignalR(options =>
{
    options.AddFilter<SignalRMetricsFilter>();
});
```

### 6.5 FusionCache Hit/Miss Metrics

Already configured via OpenTelemetry in `Program.cs`:
```csharp
x.AddFusionCacheInstrumentation(o =>
{
    o.IncludeMemoryLevel = true;
    o.IncludeDistributedLevel = true;
    o.IncludeBackplane = true;
});
```

After fixing the 2ms TTL, these metrics will show meaningful hit rates.

### 6.6 Request Tracing / Correlation IDs

Already configured with OpenTelemetry tracing. Enhance with:
```csharp
// Add game ID as trace tag for game-related requests
Activity.Current?.SetTag("game.id", gameId.ToString());
Activity.Current?.SetTag("game.phase", game.CurrentPhase);
Activity.Current?.SetTag("game.player_count", game.GamePlayers.Count);
```

### 6.7 Verification Steps

1. **Before changes:** Enable EF Core command logging, play one hand, count total DB queries
2. **After FusionCache fix:** Check cache hit rate in OpenTelemetry — should be >80% for static queries
3. **After broadcast optimization:** Count queries per broadcast — should drop from 8+ to 2-3
4. **After background service fix:** Monitor idle query rate — should be 1 per second max, not 5+
5. **After in-memory state:** Count queries per player action — should be 0-1 during active play

---

## 7. Most Likely Root Cause

The single most impactful issue is the **FusionCache 2ms TTL** combined with the **full-state-rebuild broadcast pattern**.

Here's what happens on every single player action today:

1. Player clicks "Bet $50" → HTTP POST to API
2. MediatR dispatches `ProcessBettingActionCommand`
3. Handler loads full game state from DB (cache miss due to 2ms TTL) → **Query 1**
4. Handler modifies state and calls `SaveChangesAsync()` → **Write 1**
5. `GameStateBroadcastingBehavior` fires `BroadcastGameStateAsync()`
6. `BuildPublicStateAsync` loads game again from DB (cache expired) → **Queries 2-3**
7. `GetPlayerUserIdsAsync` queries DB → **Query 4**
8. For each of 6 players, `BuildPrivateStateAsync` queries DB → **Queries 5-10**
9. All 6 clients receive full state, render, no further queries

**Total: ~10 DB queries + 1 write for a single bet.**

During an active 6-player hand with rapid betting, this can mean 60+ queries just for the betting round.

The root cause is architectural: **SQL Server is being used as the real-time game state engine**, and the caching layer that was supposed to mitigate this is effectively disabled.

---

## 8. Best Target Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        BLAZOR WEB (Clients)                      │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐        │
│  │ Player 1 │  │ Player 2 │  │ Player 3 │  │ Player N │        │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘        │
│       │ SignalR (deltas + snapshots on connect)   │              │
│       └──────────────┬───────────────────────────┘              │
└──────────────────────┼──────────────────────────────────────────┘
                       │
┌──────────────────────┼──────────────────────────────────────────┐
│                  ASP.NET API SERVER                               │
│                      │                                           │
│  ┌───────────────────▼───────────────────────┐                  │
│  │           GameStateManager                 │                  │
│  │  ConcurrentDictionary<Guid, GameState>     │ ◀── HOT STATE   │
│  │  - cards, bets, pots, phase, turn          │                  │
│  │  - NO DB reads during active play          │                  │
│  │  - Broadcasts deltas via SignalR            │                  │
│  └─────────┬─────────────────────┬────────────┘                  │
│            │                     │                               │
│   On hand complete:     On checkpoint:                           │
│   flush to DB           write snapshot                           │
│            │                     │                               │
│  ┌─────────▼─────────────────────▼────────────┐                  │
│  │         Background Workers                  │                  │
│  │  - HandHistoryWriter (queue-based)          │                  │
│  │  - ChipSettlementBatcher                    │                  │
│  │  - GameCheckpointWriter                     │                  │
│  └─────────┬──────────────────────────────────┘                  │
│            │                                                     │
└────────────┼─────────────────────────────────────────────────────┘
             │
┌────────────▼──────┐    ┌──────────────┐
│   SQL Server      │    │    Redis      │
│   (durable store) │    │  (L2 cache,  │
│   - User accounts │    │   backplane, │
│   - Hand history  │    │   pub/sub)   │
│   - Chip balances │    └──────────────┘
│   - Leagues       │
│   - Game configs  │
└───────────────────┘
```

**Key principles:**
1. **In-memory during play, durable on completion** — game actions operate on in-memory state
2. **Checkpoint, don't persist** — write to DB only on phase transitions and hand completion
3. **Push deltas, not snapshots** — SignalR sends only what changed
4. **Cache static data aggressively** — game rules, user profiles, available games
5. **Queue non-critical writes** — hand history, betting records → background workers
6. **Redis for scale-out** — backplane for SignalR, distributed state for multi-instance

---

## 9. How to Preserve UX While Scaling

### Principle: Reduce Backend Work, Not Client Updates

Every optimization preserves or improves the client experience:

| Optimization | Backend Change | Client Change | UX Effect |
|-------------|---------------|---------------|-----------|
| Fix cache TTL | Cache serves reads | None | Faster responses |
| Batch queries | Fewer DB round-trips | None | Faster broadcasts |
| In-memory state | No DB during play | None | Much faster actions |
| Delta updates | Less data to build | Process smaller payloads | Same visual result, less bandwidth |
| Background history writes | Defer non-visible writes | None | No visible difference |
| Single-query broadcasts | One DB call, not N | None | Faster state updates |

### What NOT to Do

- ❌ **Don't throttle broadcasts** — players must see actions instantly
- ❌ **Don't batch player actions** — each bet/fold must be processed immediately
- ❌ **Don't cache game state with a stale TTL** — use invalidation-on-write instead
- ❌ **Don't make the timer client-only** — server timer is the source of truth for auto-fold
- ❌ **Don't reduce SignalR connection frequency** — persistent connections are the right model

### What TO Do

- ✅ **Send less data per update** — deltas instead of full state
- ✅ **Build state faster** — fewer queries, in-memory lookups
- ✅ **Cache aggressively for static data** — game types, rules, user profiles
- ✅ **Invalidate immediately on mutations** — no stale cache served
- ✅ **Defer non-visible writes** — hand history → background queue

---

## 10. First 10 Changes I Would Make

| Priority | Change | Effort | Impact |
|----------|--------|--------|--------|
| 1 | **Fix FusionCache TTL from 2ms to 5 minutes** for static data | 5 min | Eliminates ~40% of read queries |
| 2 | **Add per-query cache overrides** for volatile game state (2-5 sec TTL) | 1 hour | Reduces repeated reads within broadcast cycles |
| 3 | **Consolidate BuildPublicStateAsync** into single query with `.AsSplitQuery()` | 2 hours | Cuts broadcast queries in half |
| 4 | **Build all private states in single batch query** in BroadcastGameStateAsync | 4 hours | Eliminates N+1 per-player queries |
| 5 | **Cache last-broadcast public state** for JoinGame reconnects | 2 hours | Eliminates DB queries on reconnect |
| 6 | **Combine background service queries** into a single query per tick | 3 hours | Reduces idle DB load by 80% |
| 7 | **Batch SaveChangesAsync calls** in InBetween, ScrewYourNeighbor, Baseball flow handlers | 4 hours | Reduces write round-trips by 60% |
| 8 | **Send timer start event once** instead of per-second broadcasts | 1 hour | Reduces SignalR volume by 50% |
| 9 | **Add explicit cache invalidation** in all command handlers after SaveChanges | 4 hours | Enables meaningful cache TTLs |
| 10 | **Add query count logging** per request for ongoing monitoring | 2 hours | Enables data-driven optimization |

---

## Answers to Specific Questions

### Am I accidentally using SQL Server as a live game engine instead of a persistence store?

**Yes.** Every player action (bet, fold, raise, draw) reads the full game state from SQL Server, modifies it, writes it back, then reads it again for broadcast. SQL Server is functioning as the real-time state engine for active poker tables. The architecture should treat SQL Server as a durable persistence store and use in-memory state for active gameplay.

### Are my Blazor components reloading state too often?

**No — the Blazor components are well-designed.** They receive state via SignalR push (not polling), use proper lifecycle methods, implement `IAsyncDisposable` for cleanup, and don't have obvious render loops. The issue is server-side: the data pushed to components is expensive to build because it requires multiple database queries.

### Are my SignalR messages causing feedback loops?

**No feedback loops detected.** The SignalR messages are one-directional: server → client for state updates, client → server for actions. Clients don't trigger server calls in response to SignalR messages (they update local state). The `GameStateBroadcastingBehavior` only fires on command success, not on queries or broadcasts.

### Should active poker table state be managed in memory per table/hand and only checkpointed to the database?

**Yes, absolutely.** This is the single most impactful architectural change. Active hand state (cards, bets, pots, phase, turn order) should live in a `ConcurrentDictionary<Guid, GameState>` in the API process. DB writes should happen only on: hand completion (settlement + history), player join/leave, and periodic checkpoints for crash recovery.

### What should be persisted immediately versus eventually?

| Immediately | Eventually |
|-------------|------------|
| Chip account balance changes | Hand history records |
| Player join/leave events | Betting action audit trail |
| Game creation/deletion | Pot contribution details |
| League membership changes | Game statistics |

### What is the safest scalable architecture for real-money-style game state consistency?

For real-money consistency:
1. **Event sourcing** for game actions — every action is an immutable event
2. **In-memory projection** of current state from events
3. **Write-ahead log** to Redis/DB before applying action (crash recovery)
4. **Optimistic concurrency** with sequence numbers per game (already using RowVersion)
5. **Idempotent action processing** with client-provided action IDs
6. **Atomic settlement** — chip transfers in a single transaction at hand completion

### How can I reduce load without making the gameplay feel less interactive?

1. Fix the cache so static data doesn't hit DB
2. Build state faster with consolidated queries
3. Cache broadcast results for reconnecting clients
4. Move active game state to memory (eliminate DB reads during play)
5. Push deltas instead of full state
6. Defer audit writes to background queues
7. Every single one of these changes makes the game feel *faster*, not slower
