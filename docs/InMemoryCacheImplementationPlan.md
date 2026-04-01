# In-Memory Game State Cache — Implementation Plan

> **Status**: Phase 0 Complete  
> **Author**: AI Architect Review  
> **Date**: 2026-03-31  
> **Related**: [docs/DatabaseCalls.md](DatabaseCalls.md)

---

## 1. Problem Statement

SQL Server is functioning as the real-time game state engine during active gameplay. Every player action (bet, fold, raise, draw) triggers **10+ database round-trips**: load full game state → modify → save → rebuild full public state → rebuild N private states for broadcast. The FusionCache configuration has a 2ms TTL (`Program.cs:145`), which effectively disables all caching.

### Current Flow Per Player Action

```
Player clicks "Bet $50" → HTTP POST to API
  → MediatR dispatches ProcessBettingActionCommand
  → Handler loads full game state from DB (cache miss, 2ms TTL)  → Query 1
  → Handler modifies state, calls SaveChangesAsync()             → Write 1
  → GameStateBroadcastingBehavior fires BroadcastGameStateAsync()
    → BuildPublicStateAsync loads game again from DB             → Queries 2-3
    → GetPlayerUserIdsAsync queries DB                           → Query 4
    → For each of 6 players, BuildPrivateStateAsync queries DB   → Queries 5-10
  → All 6 clients receive full state via SignalR
```

**Total: ~10 DB queries + 1 write for a single bet.** During a 6-player betting round with rapid action, this means 60+ queries just for one round.

### Root Cause

The root cause is architectural: SQL Server is being used as a real-time message bus, and the caching layer meant to mitigate this is effectively disabled.

### 1.1 Verified Current Code State (2026-03-31)

The items below are verified from the current codebase and should be treated as the implementation baseline for this plan:

- `src/CardGames.Poker.Api/Program.cs` configures FusionCache / HybridCache with a **2ms** default TTL. This is still present; the cache is effectively disabled unless a handler overrides expiration explicitly.
- There is **no existing `Services/Cache` folder** and no `IActiveGameCache` / `ActiveGameCache` implementation yet.
- `GameStateBroadcaster.BroadcastGameStateAsync` currently does exactly this: `BuildPublicStateAsync` → group `TableStateUpdated` broadcast → `GetPlayerUserIdsAsync` → `BuildPrivateStateAsync` once per player → per-user `PrivateStateUpdated` sends.
- `GameHub` currently depends only on `ITableStateBuilder`. `JoinGame` always calls `SendStateSnapshotToCallerAsync`, which rebuilds public state and private state from the database.
- `ITableStateBuilder` currently exposes only `BuildPublicStateAsync`, `BuildPrivateStateAsync`, and `GetPlayerUserIdsAsync`. There is no batch snapshot API today.
- `TableStateBuilder.BuildPrivateStateAsync` calls `_walletService.GetBalanceAsync(...)`, which means each private-state build can trigger an additional `PlayerChipAccounts` read beyond the game/player/card query.
- `ActionTimerService` currently broadcasts `ActionTimerUpdated` every second. The current Blazor client handler in `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` simply assigns the DTO; it does **not** yet run a client-local countdown loop derived from `StartedAtUtc` and `DurationSeconds`.
- `DeleteGameCommandHandler` currently invalidates HybridCache tags for active-games queries only. There is no active-game snapshot eviction because that cache does not exist yet.
- Existing unit-test coverage is light but useful: `src/Tests/CardGames.Poker.Tests/Api/Services/GameStateBroadcasterTests.cs` already tests broadcaster timer behavior using xUnit + NSubstitute. There are currently no unit tests for a game snapshot cache or hub reconnect caching.

---

## 2. Proposed Solution: `ConcurrentDictionary<Guid, GameState>` In-Memory Cache

### 2.1 Target Architecture

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

### 2.2 Key Design Principles

1. **In-memory during play, durable on completion** — game actions operate on in-memory state
2. **Checkpoint, don't persist** — write to DB only on phase transitions and hand completion
3. **Push deltas, not snapshots** — SignalR sends only what changed
4. **Cache static data aggressively** — game rules, user profiles, available games (5+ minute TTL)
5. **Queue non-critical writes** — hand history, betting records → background workers
6. **Redis for scale-out** — backplane for SignalR, distributed state for multi-instance

### 2.3 Non-Negotiable Invariants

These rules should not be violated in any phase:

1. **Phase 1 is read-side only.** The snapshot cache is never used as the source of truth for game mutations.
2. **Never store EF-tracked entities in memory for hot-state usage.** Phase 1 stores DTO snapshots only. Phase 4 must store detached runtime models, not live `CardsDbContext` entities.
3. **Cache writes happen only after a successful DB-backed state build.** Never mutate the cache first and “hope” the database write succeeds later.
4. **Replace whole snapshots atomically.** Do not mutate cached DTO graphs in place after insertion; always build a new snapshot and swap it in.
5. **Reject older snapshots.** Cache writes must carry a monotonic version derived from the game row version, not just `DateTimeOffset.UtcNow`, so overlapping broadcasts cannot overwrite newer state with older state.
6. **Cache miss must always degrade safely to the database.** The cache is an optimization layer, never a correctness dependency in Phase 1-3.
7. **Private-state cache keys must match SignalR user-routing semantics exactly.** If `Clients.User(userId)` and `Context.UserIdentifier` use a different identifier than the cache, cache hit rate and correctness will be unstable.

### 2.4 Rollout Guardrails

Every phase should have a configuration kill switch so the system can fall back without reverting code. The minimum options contract for Phase 1-3 should look like this:

```csharp
public sealed class ActiveGameCacheOptions
{
    public const string SectionName = "ActiveGameCache";

    public bool Enabled { get; init; } = true;
    public bool ServeReconnectsFromCache { get; init; } = true;
    public TimeSpan IdleEvictionAfter { get; init; } = TimeSpan.FromHours(1);
    public TimeSpan MaxSnapshotAge { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan ScavengeInterval { get; init; } = TimeSpan.FromMinutes(5);
}
```

Implementation notes:

- Register `TimeProvider.System` so the cache, eviction service, and tests can share deterministic time semantics.
- New behavior should be disabled by configuration in production until metrics confirm cache hit rate, no stale-state regressions, and correct reconnect behavior.
- Add a separate flag for timer-broadcast reduction; do not tie it to the snapshot cache flag.

---

## 3. Implementation Phases

### Phase 0: Quick Wins (No Architecture Changes)

**Effort: 1-2 hours | Impact: Eliminates ~40-60% of redundant reads**

| Change | File(s) | Effort | Impact |
|--------|---------|--------|--------|
| Fix FusionCache TTL from 2ms to 5 minutes | `Program.cs:145` | 5 min | ~40% read reduction |
| Add per-query cache overrides for volatile game state (2-5 sec TTL) | Query handlers | 1 hour | Reduces repeated reads within broadcast cycles |
| Add query count logging per request | Middleware + `DbCommandInterceptor` | 2 hours | Enables data-driven optimization |

#### Phase 0 Exact Work

1. Fix the FusionCache default TTL in `Program.cs`, but do not stop there. A 5-minute global default is only safe if volatile queries opt down explicitly.
2. Introduce a small invalidation service or helper so mutation flows can invalidate all per-game query tags consistently instead of having each handler guess which tags to remove.
3. Standardize volatile query tags by game ID. At minimum:
    - `game:{gameId}`
    - `game-players:{gameId}`
    - `betting-round:{gameId}`
    - `draw-player:{gameId}`
    - `active-games`
4. Continue allowing long-lived cache for static or quasi-static reads such as available game metadata. `GetAvailablePokerGamesQueryHandler` already uses an explicit 10-minute expiration and is the model to follow.
5. Implement request-scoped query counting with an EF Core interceptor plus HTTP middleware or MediatR behavior. Middleware alone cannot count SQL statements accurately.

### Phase 1: Broadcast Cache Layer

**Effort: 1-2 days | Impact: Eliminates reconnect DB queries, reduces broadcast overhead**

Create an in-memory cache that stores the last-broadcast state per game, so reconnecting clients and repeated broadcasts don't hit the database.

#### Components to Create

| Component | Location | Purpose |
|-----------|----------|---------|
| `IActiveGameCache` | `Services/Cache/IActiveGameCache.cs` | Interface: `Set`, `TryGet`, `Evict`, `GetActiveGameIds`, `Count` |
| `ActiveGameCache` | `Services/Cache/ActiveGameCache.cs` | Singleton with `ConcurrentDictionary<Guid, CachedGameSnapshot>` |
| `CachedGameSnapshot` | `Services/Cache/CachedGameSnapshot.cs` | Holds `TableStatePublicDto`, per-player `PrivateStateDto` dictionary, player user IDs, timestamp, hand number |
| `ActiveGameCacheOptions` | `Services/Cache/ActiveGameCacheOptions.cs` | Feature flags, age limits, and scavenging intervals |
| `ActiveGameCacheEvictionService` | `Services/Cache/ActiveGameCacheEvictionService.cs` | Background cleanup for stale or abandoned snapshot entries |

#### Required Contracts

The Phase 1 contracts need to be more precise than the high-level summary above. Recommended shape:

```csharp
public sealed class CachedGameSnapshot
{
    public required Guid GameId { get; init; }
    public required ulong VersionNumber { get; init; } // derived from Game.RowVersion
    public required TableStatePublicDto PublicState { get; init; }
    public required ImmutableDictionary<string, PrivateStateDto> PrivateStatesByUserId { get; init; }
    public required ImmutableArray<string> PlayerUserIds { get; init; }
    public required int HandNumber { get; init; }
    public required string Phase { get; init; }
    public required DateTimeOffset BuiltAtUtc { get; init; }
}

public interface IActiveGameCache
{
    bool TryGet(Guid gameId, out CachedGameSnapshot snapshot);
    void Set(CachedGameSnapshot snapshot);
    void UpsertPrivateState(Guid gameId, string userId, PrivateStateDto privateState, ulong versionNumber);
    bool Evict(Guid gameId);
    int Compact(DateTimeOffset olderThanUtc);
    IReadOnlyCollection<Guid> GetActiveGameIds();
    int Count { get; }
}
```

Implementation detail:

- `VersionNumber` should be derived from SQL Server `rowversion` on `Game`, not from timestamps. The cache must reject writes older than the currently stored version.
- Because the current `ITableStateBuilder` does not return row-version metadata, Phase 1 can use a **small additional metadata query** (`Games.Select(g => new { g.RowVersion, g.CurrentHandNumber, g.CurrentPhase })`) to keep the risk low. Phase 2 should fold this metadata into the batch builder.
- The cache should store immutable collections for dictionaries/lists even if DTO payload objects themselves remain mutable reference types.

#### Integration Points

| Where | Change |
|-------|--------|
| `Program.cs` | Register `IActiveGameCache` / `ActiveGameCache` as singleton |
| `GameStateBroadcaster.BroadcastGameStateAsync` | After building state from DB, store snapshot in cache |
| `GameStateBroadcaster.BroadcastGameStateToUserAsync` | Serve cached state for reconnecting clients |
| `GameHub.SendStateSnapshotToCallerAsync` | Check cache before querying DB for reconnecting clients |
| `DeleteGameCommandHandler` | Evict cache entry on game deletion |
| `ContinuousPlayBackgroundService` | Evict cache when a game is completed, abandoned, or transitions to a terminal state |

#### Identity Normalization Requirement

This is easy to get wrong. Today the system uses a mix of email, player name, and external ID fallback when deriving SignalR user IDs. Before caching private states, centralize that logic in one place and reuse it from:

- `ITableStateBuilder.GetPlayerUserIdsAsync`
- `GameStateBroadcaster.BroadcastGameStateToUserAsync`
- `GameHub.SendStateSnapshotToCallerAsync`
- `SignalRUserIdProvider`

If this is left duplicated, cache hits will be nondeterministic and users may miss their private snapshot even though it exists.

#### Exact Phase 1 Read / Write Flow

1. `BroadcastGameStateAsync` builds public state using the existing builder.
2. It loads the lightweight game metadata needed for cache versioning (`RowVersion`, `CurrentHandNumber`, `CurrentPhase`).
3. It resolves player user IDs once.
4. It builds private states for each distinct user ID using the existing path.
5. It creates a `CachedGameSnapshot` and writes it to `IActiveGameCache` using compare-and-swap semantics on `VersionNumber`.
6. It sends `TableStateUpdated` to the game group and `PrivateStateUpdated` to each user.
7. `GameHub.JoinGame` first attempts cache read. If the public snapshot exists but the caller’s private state is missing, it should:
   - send cached public state immediately
   - fall back to `BuildPrivateStateAsync` for the single user
   - `UpsertPrivateState` into the cache for future reconnects
8. If the cache has no entry, the hub falls back entirely to the current database path and optionally seeds the cache.

Recommended broadcaster skeleton:

```csharp
public async Task BroadcastGameStateAsync(Guid gameId, CancellationToken ct)
{
    var publicState = await _tableStateBuilder.BuildPublicStateAsync(gameId, ct);
    if (publicState is null)
    {
        _activeGameCache.Evict(gameId);
        return;
    }

    var metadata = await LoadSnapshotMetadataAsync(gameId, ct);
    var playerUserIds = (await _tableStateBuilder.GetPlayerUserIdsAsync(gameId, ct))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    var privateStates = new Dictionary<string, PrivateStateDto>(StringComparer.OrdinalIgnoreCase);
    foreach (var userId in playerUserIds)
    {
        var privateState = await _tableStateBuilder.BuildPrivateStateAsync(gameId, userId, ct);
        if (privateState is not null)
        {
            privateStates[userId] = privateState;
        }
    }

    _activeGameCache.Set(new CachedGameSnapshot
    {
        GameId = gameId,
        VersionNumber = metadata.VersionNumber,
        PublicState = publicState,
        PrivateStatesByUserId = privateStates.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase),
        PlayerUserIds = playerUserIds.ToImmutableArray(),
        HandNumber = metadata.HandNumber,
        Phase = metadata.Phase,
        BuiltAtUtc = _timeProvider.GetUtcNow()
    });

    await _hubContext.Clients.Group(GetGroupName(gameId)).SendAsync("TableStateUpdated", publicState, ct);
    foreach (var (userId, privateState) in privateStates)
    {
        await _hubContext.Clients.User(userId).SendAsync("PrivateStateUpdated", privateState, ct);
    }
}
```

#### Cache Lifecycle

- **Write-through**: Every `BroadcastGameStateAsync` call stores the snapshot after building from DB
- **Read on reconnect**: `BroadcastGameStateToUserAsync` and `GameHub.JoinGame` serve from cache
- **Evict**: On game deletion, game completion, or explicit invalidation
- **Overwrite**: Active games naturally overwrite on each broadcast cycle

Additional lifecycle rules:

- If a broadcast build returns `null` public state, evict the cache entry immediately.
- If a game transitions to a terminal state (`Completed`, `Cancelled`, deleted), evict the snapshot after the terminal broadcast.
- Keep snapshot eviction age-based as a secondary safety net, not the primary consistency mechanism.
- Metrics to emit from Day 1: `active_game_cache_hit`, `active_game_cache_miss`, `active_game_cache_evicted`, `active_game_cache_rejected_stale_write`, `active_game_cache_partial_private_fallback`.

### Phase 2: Consolidated Broadcast Queries

**Effort: 2-4 days | Impact: Cuts broadcast DB queries by 50-70%**

| Change | Details |
|--------|---------|
| Consolidate `BuildPublicStateAsync` into single query with `.AsSplitQuery()` | Replace multiple independent queries with one EF Core query using filtered includes |
| Build all private states in single batch query | Replace N+1 per-player `BuildPrivateStateAsync` with single query that loads all player cards, then distributes from memory |
| Add `BuildFullStateAsync` method to `ITableStateBuilder` | Returns the public DTO, private DTOs, player IDs, and snapshot metadata in one call |

#### Important Clarification

The goal of Phase 2 is not necessarily **one literal SQL statement for everything**. The current public/private builders also need user profiles, hand history, community cards, and wallet balances. The real target is:

- one bounded query for the core game aggregate (`Game`, `GameType`, `Pots`, `GamePlayers`, current-hand cards)
- one bounded query for user profiles
- one bounded query for wallet balances for all players in the hand
- one bounded query for community cards / hand history if still required on every broadcast

That is a fixed small number of queries, not an N+1 pattern.

#### Exact Interface Evolution

Phase 2 should replace the Phase 1 “extra metadata query” with a dedicated batch builder contract:

```csharp
public sealed record BroadcastStateBuildResult(
    TableStatePublicDto PublicState,
    IReadOnlyDictionary<string, PrivateStateDto> PrivateStatesByUserId,
    IReadOnlyList<string> PlayerUserIds,
    ulong VersionNumber,
    int HandNumber,
    string Phase);

public interface ITableStateBuilder
{
    Task<BroadcastStateBuildResult?> BuildFullStateAsync(Guid gameId, CancellationToken cancellationToken = default);
}
```

#### Exact Work Items

1. Load the game aggregate once and stop re-querying `Game` separately for public and private builders.
2. Batch-load player wallet balances instead of calling `PlayerChipWalletService.GetBalanceAsync` once per player.
3. Batch-load player identities / profile metadata once instead of re-deriving them in multiple builder paths.
4. Keep `BuildPublicStateAsync` and `BuildPrivateStateAsync` temporarily as thin wrappers around the new batch loader so the rest of the codebase does not have to move all at once.
5. Only after the batch builder is proven correct should `GameStateBroadcaster` stop using the older methods entirely.

```csharp
// Target: bounded query count, no per-player DB fan-out
var game = await _context.Games
    .Include(g => g.GameType)
    .Include(g => g.Pots)
    .Include(g => g.GamePlayers.Where(gp => gp.Status != GamePlayerStatus.Left))
        .ThenInclude(gp => gp.Player)
    .Include(g => g.GamePlayers.Where(gp => gp.Status != GamePlayerStatus.Left))
        .ThenInclude(gp => gp.Cards)
    .AsSplitQuery()
    .AsNoTracking()
    .FirstOrDefaultAsync(g => g.Id == gameId, ct);

var playerIds = game.GamePlayers.Select(gp => gp.PlayerId).Distinct().ToArray();

var walletBalances = await _context.PlayerChipAccounts
    .Where(a => playerIds.Contains(a.PlayerId))
    .AsNoTracking()
    .ToDictionaryAsync(a => a.PlayerId, a => a.Balance, ct);
```

### Phase 3: Background Service Optimization

**Effort: 1-2 days | Impact: Reduces idle DB load by ~80%**

| Change | Details |
|--------|---------|
| Combine `ContinuousPlayBackgroundService` queries into single query per tick | Single WHERE clause with OR conditions instead of 5 separate queries |
| Make polling adaptive | Use a fast path when active snapshots exist and a slower discovery query when they do not |
| Send timer start event once instead of per-second broadcasts | Requires a web-client local countdown first; server becomes reset/expiry authority |

#### Important Clarifications

1. `IActiveGameCache.Count == 0` is **not sufficient** to skip database polling forever. After a restart, deployment, or cold start, there may be active games in SQL and zero snapshots in memory. The correct behavior is:
    - fast 1-second loop while active cached games exist
    - slow discovery query every 15-30 seconds when the cache is empty
2. The timer optimization cannot be implemented server-side only. Today the web client still depends on `ActionTimerUpdated` pushes as the source of display state.

#### Exact Timer Rollout

1. Update `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` so the UI computes remaining seconds locally from `StartedAtUtc` and `DurationSeconds`.
2. Keep `ActionTimerUpdated` as the authoritative **reset** event and `IsActive=false` as the authoritative **stop** event.
3. Optionally keep a 5-second sync heartbeat behind a flag for one release as protection against client clock drift.
4. Only after the client has a stable local countdown should `ActionTimerService` stop broadcasting every second.

### Phase 4: Full In-Memory Game Engine (Future)

**Effort: 2-4 weeks | Impact: Eliminates all DB reads during active play**

This phase converts command handlers to operate on in-memory state instead of loading from DB on every action. This is the largest change and requires careful design.

| Component | Change |
|-----------|--------|
| `GameStateManager` singleton | Manages `ConcurrentDictionary<Guid, GameState>` with game entity + all related data |
| Command handlers | Read from `GameStateManager` instead of `CardsDbContext`; write mutations to in-memory state |
| Checkpoint service | Background worker that periodically flushes dirty game state to DB |
| Hand completion flush | Synchronous DB write on hand end for chip settlements, hand history |
| Game load | On first access, load from DB into memory; serve from memory thereafter |

#### Critical Correction

`GameStateManager` must **not** store tracked EF Core entities or `DbContext`-attached graphs. It should store a detached runtime model, for example `ActiveGameRuntimeState`, that is safe to share across requests and threads.

#### Hard Prerequisites Before Phase 4

1. Introduce a single-writer coordination service per game.
2. Add idempotency keys for client actions so retries cannot double-apply an in-memory mutation.
3. Define the append-only action log and checkpoint schema before moving the source of truth.
4. Prove crash recovery from checkpoint + event replay in automated tests before production rollout.
5. Decide explicitly whether settlement writes remain synchronous on hand completion or move to a durable queue with exactly-once semantics.

Recommended pre-Phase-4 coordinator contract:

```csharp
public interface IGameExecutionCoordinator
{
    Task<T> ExecuteAsync<T>(Guid gameId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
```

This can be introduced before Phase 4 and used first to serialize risky existing DB-backed command paths.

---

## 4. Pros and Cons

### Pros

1. **Massive DB load reduction**: Phase 0-1 alone eliminates 50%+ of queries with minimal code changes
2. **Instant reconnection**: Cached broadcast state means zero DB queries for SignalR reconnects
3. **Clear migration path**: Each phase builds on the previous, can stop at any phase
4. **Thread-safe by design**: `ConcurrentDictionary` is designed for concurrent reads/writes
5. **Single-instance simplicity**: No Redis dependency for single-server deployment
6. **Bounded memory**: Cache is bounded by active game count (not total games)

### Cons

1. **Memory usage**: Each cached game snapshot adds ~10-50KB in memory; 100 active games ≈ 1-5MB (acceptable)
2. **Stale data risk**: Cache must be invalidated on every mutation; missed invalidation = stale state shown to players
3. **Server restart = cache loss**: All cached state lost on deploy/restart; graceful degradation falls through to DB
4. **Horizontal scaling limitation**: `ConcurrentDictionary` is per-process; multi-instance requires Redis or sticky sessions
5. **Complexity**: Another layer to reason about in debugging and testing

---

## 5. Risks and Failure Modes

### 5.1 Data Loss on Server Crash

**Risk**: If game state is held only in memory and the server crashes, the current hand state is lost.

**Mitigation (Phase 1)**: The broadcast cache only caches the *output* of state builds, not the *source of truth*. Command handlers still read/write SQL. Data loss is impossible in Phase 1.

**Mitigation (Phase 4)**: When game state moves fully to memory, add:
- **Phase-transition checkpoints**: Write to DB on every phase change (deal → betting → draw → showdown)
- **Event log**: Append every player action to an in-memory log that gets flushed to DB. On crash recovery, replay the log
- **Heartbeat snapshots**: Background worker writes dirty game state to DB every 30 seconds

### 5.2 Stale State Shown to Players

**Risk**: Cache not invalidated after mutation → player sees old cards/chips/phase.

**Mitigation**: The broadcast cache is write-through — every `BroadcastGameStateAsync` overwrites the cached snapshot. Since the MediatR pipeline calls `BroadcastGameStateAsync` after every successful `IGameStateChangingCommand`, the cache is always updated immediately after any mutation.

Additional required mitigation:

- Cache writes must be version-checked so overlapping broadcasts cannot write an older snapshot after a newer one.
- Partial cache hits are acceptable. If the caller’s private snapshot is missing, send the cached public state and rebuild only the missing private state.

**Watch out for**:
- Commands that modify game state but don't implement `IGameStateChangingCommand`
- Direct DB modifications outside the MediatR pipeline (e.g., background services)
- Race conditions where two commands execute concurrently for the same game

### 5.3 Concurrency / Race Conditions

**Risk**: Two players act simultaneously on the same game; both read the same state, both modify, one overwrites the other.

**Current reality**: This risk already exists today — the codebase has no concurrency controls on game actions (see `docs/SecurityReview.md` SEC-CHEAT-02). The cache does not make this worse.

**Mitigation**: Before Phase 4, add per-game locks:
```csharp
// Introduce this before the full in-memory engine; it is useful even while SQL remains the source of truth.
private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

public async Task<T> ExecuteWithLockAsync<T>(Guid gameId, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
{
    var semaphore = _locks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
    await semaphore.WaitAsync(cancellationToken);
    try
    {
        return await action(cancellationToken);
    }
    finally
    {
        semaphore.Release();
    }
}
```

This should start as a dedicated coordinator service rather than being embedded directly into `GameStateManager`, so the codebase can adopt single-writer execution before the full architectural migration.

### 5.4 Memory Leaks

**Risk**: Games that complete or are abandoned but never evicted from cache.

**Mitigation**:
- Evict on game deletion (already in plan)
- Evict on game status change to `Completed` or `Cancelled`
- Add a background scavenger that evicts entries older than 1 hour with no updates
- Log cache size metrics for monitoring

### 5.5 Horizontal Scaling

**Risk**: `ConcurrentDictionary` is per-process. With multiple API instances, each has its own cache → inconsistent state.

**Mitigation options** (choose one when scaling out):
1. **Sticky sessions**: Route all requests for a game to the same instance (simplest)
2. **Redis-backed cache**: Replace `ConcurrentDictionary` with Redis via the `IActiveGameCache` interface
3. **Redis pub/sub**: Broadcast cache invalidation events across instances

The `IActiveGameCache` interface makes option 2 a drop-in replacement with no consumer changes.

Important nuance: per-instance snapshot caching is still safe without Redis because misses can fall back to SQL. The bigger existing scale-out limitation is actually SignalR group and user delivery without a backplane. Do not treat the cache as the only multi-instance concern.

---

## 6. What Should NOT Be Deferred to SQL Server

The following **must persist to DB immediately** (not deferred):
- Player join/leave events (seat assignments affect game logic)
- Chip balance changes on hand completion (financial integrity)
- Hand history records (audit trail)
- Game creation/deletion
- Player chip wallet deposits/withdrawals

The following **can be deferred or cached**:
- In-hand game state (current phase, current bet, cards dealt)
- Betting action records (can be written in batch at hand end)
- Timer state (already in memory)
- Last-broadcast state for reconnection

---

## 7. Do We Need Intermediate Checkpoints?

**Answer: Yes, but only in Phase 4.**

In Phase 1 (broadcast cache), SQL Server remains the source of truth. Every command handler reads from DB, modifies, and writes back. The cache only stores broadcast output. No checkpoint mechanism is needed.

In Phase 4 (full in-memory engine), intermediate checkpoints are **critical**:

| Checkpoint Trigger | What to Write | Why |
|--------------------|---------------|-----|
| Phase transition (deal → betting → draw → showdown) | Full game state snapshot | Crash recovery can resume from last phase |
| Every N actions (e.g., every 5 betting actions) | Action log batch | Limits data loss window to 5 actions |
| Hand completion | Full hand result + chip settlements | Financial integrity |
| Player join/leave | Player status change | Seat state must be durable |

**Recommended**: Event sourcing pattern — log every player action to an append-only table, replay on recovery.

---

## 8. Implementation Priority Recommendation

```
Phase 0 (Quick Wins)     → Do immediately, 1-2 hours, ~40% reduction
Phase 1 (Broadcast Cache) → Do next, 1-2 days, eliminates reconnect queries
Phase 2 (Query Consolidation) → Follow up, 2-4 days, cuts broadcast queries 50-70%
Phase 3 (Background Optimization) → Parallel with Phase 2, 1-2 days, reduces idle load 80%
Phase 4 (Full In-Memory Engine) → Plan carefully, 2-4 weeks, eliminates all in-play DB reads
```

**Recommendation**: Start with Phase 0 and Phase 1. They deliver the highest impact-to-effort ratio and carry the lowest risk. Phase 4 should be planned as a separate project with thorough design review.

---

## 9. Testing Strategy

| Phase | Testing Approach |
|-------|-----------------|
| Phase 0 | Verify FusionCache TTL change doesn't break existing behavior; run full test suite |
| Phase 1 | Unit tests for `ActiveGameCache` (set/get/evict/count/isolation); unit tests for broadcaster cache integration; integration tests for reconnect behavior |
| Phase 2 | Integration tests verifying consolidated queries return identical state to current N+1 pattern |
| Phase 3 | Load tests comparing DB query count before/after; verify background service still processes games correctly |
| Phase 4 | Full end-to-end tests for every game variant; crash recovery tests; concurrency tests with multiple simultaneous actions |

### 9.1 Existing Test Patterns to Reuse

- Service tests live under `src/Tests/CardGames.Poker.Tests/Api/Services`.
- The repository already uses xUnit + NSubstitute there, and the tests project also includes FluentAssertions for richer state assertions.
- `GameStateBroadcasterTests` is the closest existing example for how to unit-test broadcaster behavior with mocked `IHubContext`, `ITableStateBuilder`, and timer services.

### 9.2 Concrete New Tests Required

#### Phase 1

- `src/Tests/CardGames.Poker.Tests/Api/Services/Cache/ActiveGameCacheTests.cs`
    - stores and retrieves snapshots by game ID
    - rejects stale `VersionNumber` writes
    - isolates private states per user ID
    - evicts correctly
    - compacts old entries correctly
- `src/Tests/CardGames.Poker.Tests/Api/Services/GameStateBroadcasterCacheTests.cs`
    - writes snapshot after successful build
    - does not cache when public state is null
    - backfills missing private state on targeted broadcast
- `src/Tests/CardGames.Poker.Tests/Api/Hubs/GameHubTests.cs`
    - `JoinGame` serves public/private state from cache on hit
    - `JoinGame` falls back to DB on miss
    - `JoinGame` serves cached public state plus rebuilt private state on partial hit

#### Phase 2

- Query-count regression tests using an EF interceptor or command logger to prove the broadcaster no longer issues N private-state queries.
- Snapshot equivalence tests to prove `BuildFullStateAsync` returns the same observable DTOs as the existing public/private builder pair.

#### Phase 3

- Web-client tests for local countdown behavior so timer optimization does not regress UX.
- Background-service tests that verify slow discovery still finds games when the cache starts empty.

### 9.3 Acceptance Gates

Do not proceed from one phase to the next until these are true:

1. Phase 1: reconnect path is proven correct on cache hit, cache miss, and partial private-state miss.
2. Phase 2: broadcaster query count is reduced to a fixed small number and DTO parity is preserved.
3. Phase 3: timer UX remains visually correct without 1-second server broadcasts.
4. Phase 4: crash recovery and duplicate-action protection are demonstrated in automated tests, not just manual testing.

## 10. Concrete Implementation Checklist by File and Commit Sequence

This section turns the phased design into an execution order that is safe to implement incrementally. The intent is to keep each commit small enough to review, revert, and validate independently.

### 10.1 Sequencing Rules

1. Do not combine Phase 0 cache-policy work with Phase 1 reconnect-cache behavior in the same commit.
2. Do not change `ITableStateBuilder` contracts in the same commit that introduces the in-memory snapshot cache. Keep Phase 1 read-side caching and Phase 2 query consolidation separate.
3. Keep all new runtime behavior behind configuration flags until the corresponding acceptance gate is met.
4. Every commit that changes runtime behavior must include the tests for that behavior in the same commit.
5. Do not start Phase 4 source-of-truth changes in the same branch as Phase 0-3. Treat Phase 4 as a separate epic.

### 10.2 Primary File Map

Use this as the canonical file map while implementing:

| Area | Files |
|------|-------|
| API composition and options | `src/CardGames.Poker.Api/Program.cs`, `src/CardGames.Poker.Api/appsettings.json`, `src/CardGames.Poker.Api/appsettings.Development.json`, `src/CardGames.Poker.Api/appsettings.Production.json` |
| Query-cache policy | `src/CardGames.Poker.Api/Features/Games/**/Queries/**/*QueryHandler.cs` |
| Broadcast snapshot cache | `src/CardGames.Poker.Api/Services/Cache/IActiveGameCache.cs`, `src/CardGames.Poker.Api/Services/Cache/ActiveGameCache.cs`, `src/CardGames.Poker.Api/Services/Cache/CachedGameSnapshot.cs`, `src/CardGames.Poker.Api/Services/Cache/ActiveGameCacheOptions.cs`, `src/CardGames.Poker.Api/Services/Cache/ActiveGameCacheEvictionService.cs` |
| Snapshot producers/consumers | `src/CardGames.Poker.Api/Services/GameStateBroadcaster.cs`, `src/CardGames.Poker.Api/Hubs/GameHub.cs`, `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`, `src/CardGames.Poker.Api/Services/ITableStateBuilder.cs` |
| SignalR user identity normalization | `src/CardGames.Poker.Api/Infrastructure/SignalRUserIdProvider.cs`, plus a new shared resolver service |
| Background and timer behavior | `src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs`, `src/CardGames.Poker.Api/Services/ActionTimerService.cs`, `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` |
| Game lifecycle eviction points | `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/DeleteGame/DeleteGameCommandHandler.cs`, `src/CardGames.Poker.Api/Infrastructure/PipelineBehaviors/GameStateBroadcastingBehavior.cs`, `src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs` |
| Existing tests to extend | `src/Tests/CardGames.Poker.Tests/Api/Services/GameStateBroadcasterTests.cs` |
| New tests to add | `src/Tests/CardGames.Poker.Tests/Api/Services/Cache/ActiveGameCacheTests.cs`, `src/Tests/CardGames.Poker.Tests/Api/Services/GameStateBroadcasterCacheTests.cs`, `src/Tests/CardGames.Poker.Tests/Api/Hubs/GameHubTests.cs`, plus Phase 2 and 3 tests described below |

### 10.3 Recommended Commit Sequence

#### Commit 1: Add cache-key, invalidation, and query-count infrastructure

**Goal**: introduce shared plumbing without changing reconnect or broadcast behavior yet.

**Add files**

- `src/CardGames.Poker.Api/Infrastructure/Caching/GameCacheKeys.cs`
- `src/CardGames.Poker.Api/Infrastructure/Caching/IGameStateQueryCacheInvalidator.cs`
- `src/CardGames.Poker.Api/Infrastructure/Caching/GameStateQueryCacheInvalidator.cs`
- `src/CardGames.Poker.Api/Infrastructure/Diagnostics/QueryCountContext.cs`
- `src/CardGames.Poker.Api/Infrastructure/Diagnostics/QueryCountingDbCommandInterceptor.cs`
- `src/CardGames.Poker.Api/Infrastructure/Middleware/QueryCountLoggingMiddleware.cs`

**Edit files**

- `src/CardGames.Poker.Api/Program.cs`
- `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/DeleteGame/DeleteGameCommandHandler.cs`
- `src/CardGames.Poker.Api/Infrastructure/PipelineBehaviors/GameStateBroadcastingBehavior.cs`

**Checklist**

- Add a single source of truth for HybridCache keys and tags.
- Standardize at least these tags: `active-games`, `game:{gameId}`, `game-players:{gameId}`, `betting-round:{gameId}`, `draw-player:{gameId}`.
- Implement an invalidation service that removes the correct tag set for a given `gameId`.
- Register the EF Core command interceptor and request-scoped query count storage.
- Log query count at the end of each HTTP request or MediatR request.
- Route `DeleteGameCommandHandler` through the invalidation service instead of directly removing only the active-games tag.
- Update `GameStateBroadcastingBehavior` so any successful `IGameStateChangingCommand` invalidates per-game query tags before broadcasting.

**Do not include in this commit**

- The 5-minute FusionCache TTL change.
- `IActiveGameCache` or reconnect-cache behavior.
- `ITableStateBuilder` contract changes.

**Exit criteria**

- The app still behaves exactly as before.
- Query counts are visible in logs/telemetry.
- Existing tests remain green.

##### Commit 1 Literal Implementation Task List

Implement Commit 1 in this exact order so the plumbing lands cleanly and later cache-policy work has a stable surface to build on.

**Task 1: add a single source of truth for cache keys and tags**

Create `src/CardGames.Poker.Api/Infrastructure/Caching/GameCacheKeys.cs` as a static helper. Keep it free of DI and free of feature-specific runtime dependencies so query handlers can call it directly in Commit 2.

Use this skeleton as the baseline:

```csharp
namespace CardGames.Poker.Api.Infrastructure.Caching;

public static class GameCacheKeys
{
    public const string ActiveGamesTag = "active-games";

    public static string BuildVersionedKey(string featureVersion, string cacheKey)
        => $"{featureVersion}-{cacheKey}";

    public static string GameTag(Guid gameId)
        => $"game:{gameId}";

    public static string GamePlayersTag(Guid gameId)
        => $"game-players:{gameId}";

    public static string BettingRoundTag(Guid gameId)
        => $"betting-round:{gameId}";

    public static string DrawPlayerTag(Guid gameId)
        => $"draw-player:{gameId}";

    public static string CurrentPlayerTurnTag(Guid gameId)
        => $"current-player-turn:{gameId}";

    public static IReadOnlyCollection<string> BuildStandardTags(
        string featureVersion,
        string featureName,
        string queryName)
        => [featureVersion, featureName, queryName];

    public static IReadOnlyCollection<string> BuildGameScopedTags(
        string featureVersion,
        string featureName,
        string queryName,
        Guid gameId)
        =>
        [
            featureVersion,
            featureName,
            queryName,
            GameTag(gameId),
            GamePlayersTag(gameId),
            BettingRoundTag(gameId),
            DrawPlayerTag(gameId),
            CurrentPlayerTurnTag(gameId)
        ];

    public static IReadOnlyCollection<string> BuildActiveGamesTags(
        string featureVersion,
        string featureName,
        string queryName)
        => [featureVersion, featureName, queryName, ActiveGamesTag];
}
```

Implementation notes:

- Do not try to encode every query-specific variation in Commit 1. The key requirement is to establish the canonical tag names and a versioned key helper.
- Include `CurrentPlayerTurnTag` now even though it is not named in the short checklist above. Commit 2 needs it immediately for the game-specific turn queries.
- Keep the tag helpers deterministic and allocation-light. This class will be called in hot query paths.

**Task 2: add the invalidation abstraction and concrete implementation**

Create `src/CardGames.Poker.Api/Infrastructure/Caching/IGameStateQueryCacheInvalidator.cs`:

```csharp
namespace CardGames.Poker.Api.Infrastructure.Caching;

public interface IGameStateQueryCacheInvalidator
{
    ValueTask InvalidateAfterMutationAsync(Guid gameId, CancellationToken cancellationToken = default);

    ValueTask InvalidateGameAsync(Guid gameId, CancellationToken cancellationToken = default);

    ValueTask InvalidateActiveGamesAsync(CancellationToken cancellationToken = default);
}
```

Create `src/CardGames.Poker.Api/Infrastructure/Caching/GameStateQueryCacheInvalidator.cs`:

```csharp
using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Infrastructure.Caching;

public sealed class GameStateQueryCacheInvalidator(
    HybridCache hybridCache,
    ILogger<GameStateQueryCacheInvalidator> logger)
    : IGameStateQueryCacheInvalidator
{
    public async ValueTask InvalidateAfterMutationAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        await InvalidateGameAsync(gameId, cancellationToken);
        await InvalidateActiveGamesAsync(cancellationToken);
    }

    public async ValueTask InvalidateGameAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        foreach (var tag in EnumerateGameTags(gameId))
        {
            await hybridCache.RemoveByTagAsync(tag, cancellationToken);
        }

        logger.LogDebug("Invalidated HybridCache tags for game {GameId}", gameId);
    }

    public async ValueTask InvalidateActiveGamesAsync(
        CancellationToken cancellationToken = default)
    {
        await hybridCache.RemoveByTagAsync(GameCacheKeys.ActiveGamesTag, cancellationToken);
    }

    private static IEnumerable<string> EnumerateGameTags(Guid gameId)
    {
        yield return GameCacheKeys.GameTag(gameId);
        yield return GameCacheKeys.GamePlayersTag(gameId);
        yield return GameCacheKeys.BettingRoundTag(gameId);
        yield return GameCacheKeys.DrawPlayerTag(gameId);
        yield return GameCacheKeys.CurrentPlayerTurnTag(gameId);
    }
}
```

Implementation notes:

- Keep `InvalidateAfterMutationAsync` as the primary call site for commands that change gameplay state.
- `InvalidateGameAsync` should not remove the global `active-games` tag; keep that separation explicit so non-lifecycle mutations can be reasoned about later if needed.
- Do not add game-snapshot eviction methods here. Snapshot cache eviction belongs to the Phase 1 `IActiveGameCache`, not HybridCache invalidation.

**Task 3: add request-scoped SQL command counting state**

Create `src/CardGames.Poker.Api/Infrastructure/Diagnostics/QueryCountContext.cs`:

```csharp
using System.Threading;

namespace CardGames.Poker.Api.Infrastructure.Diagnostics;

public sealed class QueryCountContext
{
    private int _commandCount;

    public int CommandCount => Volatile.Read(ref _commandCount);

    public void Increment()
        => Interlocked.Increment(ref _commandCount);

    public int Reset()
        => Interlocked.Exchange(ref _commandCount, 0);
}
```

Implementation notes:

- Register this as `Scoped`, not `Singleton`. The count must be isolated to the current request scope.
- Keep it intentionally small. It only needs to count SQL command executions for this phase.
- Count retries as separate executions. That gives a truer picture of actual database load.

**Task 4: add the EF Core interceptor that increments the query count**

Create `src/CardGames.Poker.Api/Infrastructure/Diagnostics/QueryCountingDbCommandInterceptor.cs`:

```csharp
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CardGames.Poker.Api.Infrastructure.Diagnostics;

public sealed class QueryCountingDbCommandInterceptor(QueryCountContext queryCountContext)
    : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        queryCountContext.Increment();
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        queryCountContext.Increment();
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        queryCountContext.Increment();
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        queryCountContext.Increment();
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        queryCountContext.Increment();
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        queryCountContext.Increment();
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}
```

Implementation notes:

- Count `Reader`, `Scalar`, and `NonQuery` operations. The goal is total SQL command count, not just reads.
- Do not try to exclude specific EF-generated commands in Commit 1. Baseline first, optimize filters later only if noise becomes a problem.
- Hook the interceptor into the existing `CardsDbContext` registration path. Do not replace the Aspire SQL registration with a plain `AddDbContext` unless the Aspire helper gives no way to add interceptors.

**Task 5: add middleware that logs the final request-level query count**

Create `src/CardGames.Poker.Api/Infrastructure/Middleware/QueryCountLoggingMiddleware.cs`:

```csharp
using System.Diagnostics;
using CardGames.Poker.Api.Infrastructure.Diagnostics;

namespace CardGames.Poker.Api.Infrastructure.Middleware;

public sealed class QueryCountLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<QueryCountLoggingMiddleware> _logger;

    public QueryCountLoggingMiddleware(
        RequestDelegate next,
        ILogger<QueryCountLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, QueryCountContext queryCountContext)
    {
        queryCountContext.Reset();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var commandCount = queryCountContext.CommandCount;

            Activity.Current?.SetTag("db.query.count", commandCount);

            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} with {SqlCommandCount} SQL commands in {ElapsedMilliseconds}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                commandCount,
                stopwatch.ElapsedMilliseconds);

            queryCountContext.Reset();
        }
    }
}
```

Implementation notes:

- Place this middleware after `ExceptionHandlingMiddleware` and `TrimStringsMiddleware`, but before authentication/authorization and endpoint execution, so it wraps the real request work and still logs when downstream code throws.
- Use a `finally` block so failed requests still produce a query-count log line.
- Do not emit response headers for this in production in Commit 1. Logging and telemetry tags are enough.

**Task 6: wire the new services into `Program.cs`**

Edit `src/CardGames.Poker.Api/Program.cs` and make these changes in one pass:

1. Add the new namespaces for caching and diagnostics.
2. Register:
   - `builder.Services.AddScoped<QueryCountContext>();`
   - `builder.Services.AddScoped<QueryCountingDbCommandInterceptor>();`
   - `builder.Services.AddScoped<IGameStateQueryCacheInvalidator, GameStateQueryCacheInvalidator>();`
3. Attach `QueryCountingDbCommandInterceptor` to the `CardsDbContext` options builder in the existing SQL registration path.
4. Insert `app.UseMiddleware<QueryCountLoggingMiddleware>();` immediately after the existing exception and trim middleware.

If the Aspire registration supports an options callback, the target shape should look conceptually like this:

```csharp
builder.Services.AddScoped<QueryCountContext>();
builder.Services.AddScoped<QueryCountingDbCommandInterceptor>();
builder.Services.AddScoped<IGameStateQueryCacheInvalidator, GameStateQueryCacheInvalidator>();

builder.AddSqlServerDbContext<CardsDbContext>("cardsdb", (sp, optionsBuilder) =>
{
    optionsBuilder.AddInterceptors(
        sp.GetRequiredService<QueryCountingDbCommandInterceptor>());
});
```

Treat the callback signature above as illustrative. The exact Aspire overload may differ, but the requirement does not: keep the existing `CardsDbContext` registration path as the single registration and attach the interceptor there through the scoped service provider made available by the registration API.

**Task 7: route existing mutation points through the invalidator**

Edit `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/DeleteGame/DeleteGameCommandHandler.cs`.

Constructor change:

```csharp
public sealed class DeleteGameCommandHandler(
    CardsDbContext context,
    ICurrentUserService currentUserService,
    ILobbyBroadcaster lobbyBroadcaster,
    IGameStateQueryCacheInvalidator gameStateQueryCacheInvalidator,
    ILogger<DeleteGameCommandHandler> logger)
    : IRequestHandler<DeleteGameCommand, OneOf<DeleteGameSuccessful, DeleteGameError>>
```

Handler change:

```csharp
await gameStateQueryCacheInvalidator.InvalidateAfterMutationAsync(command.GameId, cancellationToken);
```

Implementation notes:

- Replace the direct `HybridCache` dependency completely in this handler.
- Keep the invalidation after `SaveChangesAsync` and after the lobby deletion broadcast only if the current UI relies on the active-games query staying warm through the lobby broadcast. Otherwise prefer invalidating immediately after `SaveChangesAsync` so later reads cannot observe stale tags. Pick one ordering and document it in the code review.

Edit `src/CardGames.Poker.Api/Infrastructure/PipelineBehaviors/GameStateBroadcastingBehavior.cs`.

Constructor change:

```csharp
public GameStateBroadcastingBehavior(
    IGameStateBroadcaster broadcaster,
    IGameStateQueryCacheInvalidator gameStateQueryCacheInvalidator,
    ILogger<GameStateBroadcastingBehavior<TRequest, TResponse>> logger)
```

New call inside `Handle(...)`, after success is confirmed and before any state broadcast:

```csharp
await gameStateQueryCacheInvalidator.InvalidateAfterMutationAsync(gameCommand.GameId, cancellationToken);
```

Implementation notes:

- Invalidate before `BroadcastGameStateAsync`, not after. The broadcaster rebuilds state from the database, so it must not see a stale HybridCache hit.
- Keep the invalidation inside the existing `try` block so failures are logged along with broadcast failures.

**Task 8: add tests in the same commit**

Add these tests even though the short commit summary above did not enumerate them explicitly:

- `src/Tests/CardGames.Poker.Tests/Api/Infrastructure/Caching/GameCacheKeysTests.cs`
- `src/Tests/CardGames.Poker.Tests/Api/Infrastructure/Caching/GameStateQueryCacheInvalidatorTests.cs`

Recommended test methods:

```csharp
public sealed class GameCacheKeysTests
{
    [Fact]
    public void BuildGameScopedTags_IncludesCanonicalPerGameTags() { }

    [Fact]
    public void BuildActiveGamesTags_IncludesActiveGamesTag() { }
}

public sealed class GameStateQueryCacheInvalidatorTests
{
    [Fact]
    public async Task InvalidateGameAsync_RemovesExpectedPerGameTags() { }

    [Fact]
    public async Task InvalidateAfterMutationAsync_RemovesPerGameAndActiveGamesTags() { }
}
```

For query counting, a unit test for `QueryCountContext` is optional, but at minimum manually verify one live API request logs a non-zero SQL command count before declaring Commit 1 done.

**Definition of done for the literal Commit 1 implementation**

- No query handler behavior changes yet.
- No TTL changes yet.
- No snapshot cache types yet.
- One gameplay mutation path and the delete path both invalidate through `IGameStateQueryCacheInvalidator`.
- Logs contain one request-level SQL command count line per HTTP request.
- The codebase now has a stable `GameCacheKeys` surface that Commit 2 can use without inventing more tag strings.

#### Commit 2: Normalize HybridCache policy and per-query TTLs

**Goal**: make HybridCache effective without serving obviously stale volatile reads.

**Edit files**

- `src/CardGames.Poker.Api/Program.cs`
- `src/CardGames.Poker.Api/Features/Games/AvailablePokerGames/v1/Queries/GetAvailablePokerGames/GetAvailablePokerGamesQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/ActiveGames/v1/Queries/GetActiveGames/GetActiveGamesQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/Common/v1/Queries/GetGame/GetGameQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/Common/v1/Queries/GetGames/GetGamesQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/Common/v1/Queries/GetGamePlayers/GetGamePlayersQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/Common/v1/Queries/GetCurrentBettingRound/GetCurrentBettingRoundQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/Common/v1/Queries/GetCurrentDrawPlayer/GetCurrentDrawPlayerQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/Baseball/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/SevenCardStud/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/TwosJacksManWithTheAxe/v1/Queries/GetCurrentPlayerTurn/GetCurrentPlayerTurnQueryHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/TwosJacksManWithTheAxe/v1/Queries/GetCurrentBettingRound/GetCurrentBettingRoundQueryHandler.cs`

**Checklist**

- Change the default FusionCache duration in `Program.cs` from `TimeSpan.FromMilliseconds(2)` to `TimeSpan.FromMinutes(5)`.
- Keep explicitly long-lived queries such as available game metadata on their current longer durations.
- For volatile game-state queries, add explicit short durations, ideally 2-5 seconds, plus the correct per-game tags from `GameCacheKeys`.
- Ensure all query handlers that use HybridCache now call into the same key/tag helpers instead of ad hoc string literals.
- Verify the mutation invalidation service from Commit 1 clears the corresponding tags.

**Do not include in this commit**

- Any in-memory reconnect cache.
- Any broadcaster query consolidation.

**Exit criteria**

- Static queries show meaningful cache hits.
- Volatile query handlers still invalidate correctly after commands.
- No reconnect or SignalR behavior has changed yet.

#### Commit 3: Introduce the in-memory snapshot cache core

**Goal**: add the cache implementation and feature flags, but do not consume it yet.

**Add files**

- `src/CardGames.Poker.Api/Services/Cache/IActiveGameCache.cs`
- `src/CardGames.Poker.Api/Services/Cache/ActiveGameCache.cs`
- `src/CardGames.Poker.Api/Services/Cache/CachedGameSnapshot.cs`
- `src/CardGames.Poker.Api/Services/Cache/ActiveGameCacheOptions.cs`
- `src/CardGames.Poker.Api/Services/Cache/ActiveGameCacheEvictionService.cs`

**Edit files**

- `src/CardGames.Poker.Api/Program.cs`
- `src/CardGames.Poker.Api/appsettings.json`
- `src/CardGames.Poker.Api/appsettings.Development.json`
- `src/CardGames.Poker.Api/appsettings.Production.json`

**Add tests**

- `src/Tests/CardGames.Poker.Tests/Api/Services/Cache/ActiveGameCacheTests.cs`

**Checklist**

- Bind `ActiveGameCacheOptions` from configuration.
- Register `TimeProvider.System` explicitly so cache expiration logic and tests use the same abstraction.
- Register `IActiveGameCache` as a singleton and the scavenger as a hosted service.
- Implement compare-and-swap semantics on `VersionNumber` so stale snapshots are rejected.
- Keep cached collections immutable at the boundary, even if DTO internals remain reference types.
- Implement `Compact` and age-based scavenging, but do not yet route hub or broadcaster reads through the cache.

**Do not include in this commit**

- Any `GameStateBroadcaster` or `GameHub` behavior change.

**Exit criteria**

- Unit tests prove stale write rejection, per-user private-state isolation, eviction, and compaction.
- Feature flags exist and can disable all new runtime behavior.

#### Commit 4: Centralize SignalR user-id normalization

**Goal**: eliminate duplicate user-id derivation before private snapshot caching is introduced.

**Add files**

- `src/CardGames.Poker.Api/Services/GameUserIdResolver.cs`
- `src/CardGames.Poker.Api/Services/IGameUserIdResolver.cs`

**Edit files**

- `src/CardGames.Poker.Api/Program.cs`
- `src/CardGames.Poker.Api/Infrastructure/SignalRUserIdProvider.cs`
- `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`
- `src/CardGames.Poker.Api/Services/GameStateBroadcaster.cs`
- `src/CardGames.Poker.Api/Hubs/GameHub.cs`

**Add tests**

- `src/Tests/CardGames.Poker.Tests/Api/Services/GameUserIdResolverTests.cs`

**Checklist**

- Extract the email/name/external-id fallback logic into one service.
- Ensure `SignalRUserIdProvider`, `GetPlayerUserIdsAsync`, `BroadcastGameStateToUserAsync`, and `GameHub.SendStateSnapshotToCallerAsync` all use the same resolver.
- Preserve existing routing semantics for legacy malformed email data.
- Make the resolver case-insensitive and deterministic.

**Do not include in this commit**

- Snapshot caching behavior.

**Exit criteria**

- The same logical user ID is produced for SignalR routing, targeted broadcasts, and cache lookups.

#### Commit 5: Write-through snapshot caching in `GameStateBroadcaster`

**Goal**: populate the cache on successful broadcasts without changing the hub reconnect path yet.

**Edit files**

- `src/CardGames.Poker.Api/Services/GameStateBroadcaster.cs`
- `src/CardGames.Poker.Api/Program.cs`

**Optional add file if needed to keep the broadcaster thin**

- `src/CardGames.Poker.Api/Services/Cache/GameSnapshotMetadataLoader.cs`

**Add tests**

- `src/Tests/CardGames.Poker.Tests/Api/Services/GameStateBroadcasterCacheTests.cs`
- `src/Tests/CardGames.Poker.Tests/Api/Services/GameStateBroadcasterTests.cs`

**Checklist**

- After a successful public/private build, load the metadata needed for snapshot versioning: `Game.RowVersion`, `CurrentHandNumber`, `CurrentPhase`.
- Resolve player user IDs once per broadcast.
- Build the per-user private-state dictionary and write a `CachedGameSnapshot` to `IActiveGameCache`.
- If `BuildPublicStateAsync` returns `null`, evict the snapshot immediately.
- In `BroadcastGameStateToUserAsync`, use a partial-hit strategy: send cached public state when available, rebuild private state only when missing, then upsert that private state into the cache.
- Emit metrics for cache hit, miss, stale-write rejection, and partial private-state fallback.

**Do not include in this commit**

- `GameHub` reconnect reads from the cache.
- `ITableStateBuilder` contract changes.

**Exit criteria**

- Broadcast flow still uses the database as the source of truth.
- Snapshot cache is populated and can be observed in tests and metrics.

#### Commit 6: Read from the snapshot cache on reconnect and add lifecycle eviction

**Goal**: use the Phase 1 cache where it provides the highest value, while ensuring terminal-state eviction is correct.

**Edit files**

- `src/CardGames.Poker.Api/Hubs/GameHub.cs`
- `src/CardGames.Poker.Api/Services/GameStateBroadcaster.cs`
- `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/DeleteGame/DeleteGameCommandHandler.cs`
- `src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs`

**Add tests**

- `src/Tests/CardGames.Poker.Tests/Api/Hubs/GameHubTests.cs`
- `src/Tests/CardGames.Poker.Tests/Api/Services/GameStateBroadcasterCacheTests.cs`

**Checklist**

- Update `GameHub.JoinGame` so it first attempts an `IActiveGameCache.TryGet(gameId, out snapshot)`.
- On full hit, send cached public state and cached private state to the caller.
- On partial hit, send cached public state immediately, rebuild only the caller private state, then `UpsertPrivateState` into the cache.
- On full miss, fall back to the current database path and optionally seed the cache.
- Evict snapshots on delete and on any background-service terminal transition to `Completed`, `Cancelled`, or equivalent abandoned state.
- Confirm that a terminal broadcast happens before eviction when the final visible state must still reach clients.

**Do not include in this commit**

- Batch query consolidation.

**Exit criteria**

- Reconnect path is correct on hit, miss, and partial private-state miss.
- Terminal games do not leak snapshots indefinitely.

#### Commit 7: Introduce the Phase 2 batch builder contract

**Goal**: define the new builder surface while preserving the old callers temporarily.

**Add files**

- `src/CardGames.Poker.Api/Services/BroadcastStateBuildResult.cs`

**Edit files**

- `src/CardGames.Poker.Api/Services/ITableStateBuilder.cs`
- `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`

**Add tests**

- `src/Tests/CardGames.Poker.Tests/Api/Services/TableStateBuilderContractTests.cs`

**Checklist**

- Add `BuildFullStateAsync(Guid gameId, CancellationToken ct)` to `ITableStateBuilder`.
- Implement `BuildPublicStateAsync` and `BuildPrivateStateAsync` as wrappers over the new internal batch pipeline where practical.
- Keep the old public methods available until `GameStateBroadcaster` is switched over in the next commit.
- Ensure `BroadcastStateBuildResult` carries `PublicState`, per-user private states, user IDs, `VersionNumber`, `HandNumber`, and `Phase`.

**Do not include in this commit**

- Broadcaster migration to `BuildFullStateAsync`.

**Exit criteria**

- The new contract compiles and is testable without changing runtime behavior broadly.

#### Commit 8: Implement bounded-query broadcast building and switch the broadcaster

**Goal**: remove N+1 private-state query fan-out during broadcasts.

**Edit files**

- `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`
- `src/CardGames.Poker.Api/Services/GameStateBroadcaster.cs`
- `src/CardGames.Poker.Api/Services/ITableStateBuilder.cs`

**Add tests**

- `src/Tests/CardGames.Poker.Tests/Api/Services/TableStateBuilderSnapshotParityTests.cs`
- `src/Tests/CardGames.Poker.Tests/Api/Services/GameStateBroadcasterQueryCountTests.cs`

**Checklist**

- Load the core game aggregate in a bounded query set.
- Batch-load wallet balances for all players instead of calling `_walletService.GetBalanceAsync(...)` per player.
- Batch-load user profile data once.
- Preserve all currently observable DTO behavior while switching the broadcaster to `BuildFullStateAsync`.
- Use query-count instrumentation to prove the broadcaster no longer issues one private-state query per player.

**Do not include in this commit**

- Timer-broadcast reduction.

**Exit criteria**

- Broadcaster query count is reduced to a fixed small number.
- DTO parity tests pass.

#### Commit 9: Add client-local timer countdown behind a flag

**Goal**: make the web client independent of 1-second timer pushes before reducing server broadcasts.

**Edit files**

- `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`
- `src/CardGames.Poker.Api/appsettings.json`
- `src/CardGames.Poker.Api/appsettings.Development.json`
- `src/CardGames.Poker.Api/appsettings.Production.json`

**Add files if no existing web test project exists**

- `src/Tests/CardGames.Poker.Web.Tests/CardGames.Poker.Web.Tests.csproj`
- `src/Tests/CardGames.Poker.Web.Tests/Components/Pages/TablePlayTimerTests.cs`

**Checklist**

- Replace the current `HandleActionTimerUpdatedAsync` assign-only behavior with local countdown logic derived from `StartedAtUtc` and `DurationSeconds`.
- Keep `ActionTimerUpdated` as the authoritative start/reset event.
- Keep `IsActive = false` as the authoritative stop event.
- Guard the local countdown with a feature flag so the legacy behavior can be restored quickly if needed.

**Do not include in this commit**

- Server-side timer tick reduction.

**Exit criteria**

- The client timer stays visually correct when no new timer message arrives every second.

#### Commit 10: Reduce `ActionTimerService` to reset/stop events only

**Goal**: cut chatty timer broadcasts after the client can count down locally.

**Add files if needed**

- `src/CardGames.Poker.Api/Services/ActionTimerOptions.cs`

**Edit files**

- `src/CardGames.Poker.Api/Services/ActionTimerService.cs`
- `src/CardGames.Poker.Api/Program.cs`
- `src/CardGames.Poker.Api/appsettings.json`
- `src/CardGames.Poker.Api/appsettings.Development.json`
- `src/CardGames.Poker.Api/appsettings.Production.json`
- `src/Tests/CardGames.Poker.Tests/Api/Services/GameStateBroadcasterTests.cs`

**Checklist**

- Add a feature flag controlling whether per-second timer broadcasts remain enabled.
- Change `StartTimer` and `StartChipCheckPauseTimer` to send the initial authoritative timer state.
- Change `OnTimerTickAsync` to stop broadcasting every second when the optimization flag is enabled.
- Preserve expiry behavior and the final `IsActive = false` stop broadcast.
- Optionally keep a low-frequency sync heartbeat behind a separate flag if clock-drift protection is needed.

**Exit criteria**

- Server no longer emits one SignalR timer update per second when the flag is enabled.
- Client UX remains correct.

#### Commit 11: Make `ContinuousPlayBackgroundService` adaptive and cache-aware

**Goal**: reduce idle polling load without making cold-start recovery blind.

**Edit files**

- `src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs`
- `src/CardGames.Poker.Api/Program.cs`
- `src/CardGames.Poker.Api/appsettings.json`
- `src/CardGames.Poker.Api/appsettings.Development.json`
- `src/CardGames.Poker.Api/appsettings.Production.json`

**Add files if needed**

- `src/CardGames.Poker.Api/Services/ContinuousPlayOptions.cs`

**Add tests**

- `src/Tests/CardGames.Poker.Tests/Api/Services/ContinuousPlayBackgroundServiceTests.cs`

**Checklist**

- Add a fast path when active cached snapshots exist.
- Add a slower discovery query interval when the snapshot cache is empty so active games are still rediscovered after restart.
- Consolidate polling queries where practical instead of issuing five separate scans every second.
- Evict snapshots when games are abandoned or moved to terminal phases.
- Emit metrics for active-cache size, discovery scans, and terminal evictions.

**Exit criteria**

- Background service still finds active games after restart.
- Idle database polling is measurably lower.

#### Commit 12: Start Phase 4 prerequisites only in a separate branch or PR stack

**Goal**: prepare for the full in-memory engine without changing the source of truth yet.

**Add files**

- `src/CardGames.Poker.Api/Services/Concurrency/IGameExecutionCoordinator.cs`
- `src/CardGames.Poker.Api/Services/Concurrency/GameExecutionCoordinator.cs`
- `src/CardGames.Poker.Api/Services/Concurrency/GameExecutionCoordinatorOptions.cs`

**Checklist**

- Introduce per-game single-writer coordination.
- Define where idempotency keys will live on player-action commands.
- Define checkpoint and event-log schema before any runtime source-of-truth shift.
- Prove crash recovery in tests before any command handler stops treating SQL as the source of truth.

**Hard rule**

- Do not move command handlers to an in-memory source of truth in the same workstream as Commits 1-11.

### 10.4 PR / Branch Grouping Recommendation

If this is implemented through pull requests rather than a single long-running branch, use this grouping:

1. PR A: Commits 1-2 (Phase 0 only).
2. PR B: Commits 3-6 (Phase 1 only).
3. PR C: Commits 7-8 (Phase 2 only).
4. PR D: Commits 9-11 (Phase 3 only).
5. PR E: Commit 12 and the rest of Phase 4 prerequisites.

This keeps rollback boundaries aligned with the architectural phases.

### 10.5 Verification Commands Per Stage

Run these at minimum after each PR or major commit series:

1. `dotnet build src/CardGames.sln`
2. `dotnet test src/CardGames.sln`
3. Focused broadcaster tests once added: `dotnet test src/CardGames.sln --filter GameStateBroadcaster`
4. Focused hub/cache tests once added: `dotnet test src/CardGames.sln --filter ActiveGameCache|GameHub`

For Phase 2 and 3, also verify runtime metrics rather than relying only on test pass/fail:

1. Cache hit rate for active-game query handlers.
2. Query count per game action before and after the commit.
3. SignalR timer message volume before and after timer optimization.

### 10.6 Explicit No-Go Combinations

Do not combine any of these into the same commit:

1. `IActiveGameCache` introduction and `BuildFullStateAsync` contract expansion.
2. Hub reconnect caching and timer optimization.
3. Background polling refactor and in-memory source-of-truth work.
4. Identity-routing normalization and any schema migration.
5. Query-count instrumentation removal or cleanup before Phase 2 baselines are captured.
