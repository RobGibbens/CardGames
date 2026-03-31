# In-Memory Game State Cache — Implementation Plan

> **Status**: Planning  
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

---

## 3. Implementation Phases

### Phase 0: Quick Wins (No Architecture Changes)

**Effort: 1-2 hours | Impact: Eliminates ~40-60% of redundant reads**

| Change | File(s) | Effort | Impact |
|--------|---------|--------|--------|
| Fix FusionCache TTL from 2ms to 5 minutes | `Program.cs:145` | 5 min | ~40% read reduction |
| Add per-query cache overrides for volatile game state (2-5 sec TTL) | Query handlers | 1 hour | Reduces repeated reads within broadcast cycles |
| Add query count logging per request | Middleware | 2 hours | Enables data-driven optimization |

### Phase 1: Broadcast Cache Layer

**Effort: 1-2 days | Impact: Eliminates reconnect DB queries, reduces broadcast overhead**

Create an in-memory cache that stores the last-broadcast state per game, so reconnecting clients and repeated broadcasts don't hit the database.

#### Components to Create

| Component | Location | Purpose |
|-----------|----------|---------|
| `IActiveGameCache` | `Services/Cache/IActiveGameCache.cs` | Interface: `Set`, `TryGet`, `Evict`, `GetActiveGameIds`, `Count` |
| `ActiveGameCache` | `Services/Cache/ActiveGameCache.cs` | Singleton with `ConcurrentDictionary<Guid, CachedGameSnapshot>` |
| `CachedGameSnapshot` | `Services/Cache/CachedGameSnapshot.cs` | Holds `TableStatePublicDto`, per-player `PrivateStateDto` dictionary, player user IDs, timestamp, hand number |

#### Integration Points

| Where | Change |
|-------|--------|
| `Program.cs` | Register `IActiveGameCache` / `ActiveGameCache` as singleton |
| `GameStateBroadcaster.BroadcastGameStateAsync` | After building state from DB, store snapshot in cache |
| `GameStateBroadcaster.BroadcastGameStateToUserAsync` | Serve cached state for reconnecting clients |
| `GameHub.SendStateSnapshotToCallerAsync` | Check cache before querying DB for reconnecting clients |
| `DeleteGameCommandHandler` | Evict cache entry on game deletion |

#### Cache Lifecycle

- **Write-through**: Every `BroadcastGameStateAsync` call stores the snapshot after building from DB
- **Read on reconnect**: `BroadcastGameStateToUserAsync` and `GameHub.JoinGame` serve from cache
- **Evict**: On game deletion, game completion, or explicit invalidation
- **Overwrite**: Active games naturally overwrite on each broadcast cycle

### Phase 2: Consolidated Broadcast Queries

**Effort: 2-4 days | Impact: Cuts broadcast DB queries by 50-70%**

| Change | Details |
|--------|---------|
| Consolidate `BuildPublicStateAsync` into single query with `.AsSplitQuery()` | Replace multiple independent queries with one EF Core query using filtered includes |
| Build all private states in single batch query | Replace N+1 per-player `BuildPrivateStateAsync` with single query that loads all player cards, then distributes from memory |
| Add `BuildFullStateAsync` method to `ITableStateBuilder` | Returns `(TableStatePublicDto, Dictionary<string, PrivateStateDto>)` in one call |

```csharp
// Target: Single query loads everything needed for broadcast
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
```

### Phase 3: Background Service Optimization

**Effort: 1-2 days | Impact: Reduces idle DB load by ~80%**

| Change | Details |
|--------|---------|
| Combine `ContinuousPlayBackgroundService` queries into single query per tick | Single WHERE clause with OR conditions instead of 5 separate queries |
| Make polling adaptive | Skip DB queries when `IActiveGameCache.Count == 0` |
| Send timer start event once instead of per-second broadcasts | Client counts down locally; server only sends on expiry |

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

**Watch out for**:
- Commands that modify game state but don't implement `IGameStateChangingCommand`
- Direct DB modifications outside the MediatR pipeline (e.g., background services)
- Race conditions where two commands execute concurrently for the same game

### 5.3 Concurrency / Race Conditions

**Risk**: Two players act simultaneously on the same game; both read the same state, both modify, one overwrites the other.

**Current reality**: This risk already exists today — the codebase has no concurrency controls on game actions (see `docs/SecurityReview.md` SEC-CHEAT-02). The cache does not make this worse.

**Mitigation**: Before Phase 4, add per-game locks:
```csharp
// GameStateManager should use SemaphoreSlim per game
private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

public async Task<T> ExecuteWithLockAsync<T>(Guid gameId, Func<GameState, Task<T>> action)
{
    var semaphore = _locks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
    await semaphore.WaitAsync();
    try
    {
        var state = _cache[gameId];
        return await action(state);
    }
    finally
    {
        semaphore.Release();
    }
}
```

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
