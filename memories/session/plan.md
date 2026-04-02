# Plan: Phase 4 — Full In-Memory Game Engine

## TL;DR
Move all active-game state from SQL Server into a per-game `ActiveGameRuntimeState` held in `GameStateManager` (singleton `ConcurrentDictionary`). Command handlers stop reading/writing `CardsDbContext` for game state mutations and instead operate on the runtime model via `IGameExecutionCoordinator` (single-writer per game). DB writes happen synchronously on hand completion (settlement + history) and on phase-transition checkpoints. On server restart, active games reload from the last checkpoint. All ~100 command handlers across 18+ game types migrate.

## Decisions
- Settlement writes: synchronous on hand completion (IHandSettlementService unchanged)
- Crash recovery: checkpoint-only (reload from last DB checkpoint, accept mid-phase loss)
- Migration scope: all ~100 handlers across all game types
- No idempotency keys or action logs in this phase (checkpoint-only recovery model)

---

## Phased Implementation

### Phase A: Foundation Infrastructure (blocking — all later phases depend on this)

**A1. ActiveGameRuntimeState model** — `src/CardGames.Poker.Api/Services/InMemoryEngine/ActiveGameRuntimeState.cs`
- Detached runtime model (NOT EF entities) holding complete game state: Game metadata, players, pots, cards, betting rounds
- Sub-records: `RuntimeGamePlayer`, `RuntimePot`, `RuntimeCard`, `RuntimeBettingRound`, `RuntimeBettingAction`
- Must include `long Version` (monotonic counter replacing RowVersion) and `bool IsDirty` flag
- Include `string GameTypeCode` for flow handler routing
- Include `GameSettings` for variant-specific config

**A2. IGameStateManager / GameStateManager** — `src/CardGames.Poker.Api/Services/InMemoryEngine/GameStateManager.cs`
- Singleton: `ConcurrentDictionary<Guid, ActiveGameRuntimeState>`
- Methods: `TryGetGame`, `LoadGameAsync` (from DB on first access/restart), `RemoveGame`, `GetActiveGameIds`, `Count`
- `LoadGameAsync` uses `CardsDbContext` to hydrate runtime state from DB (including all GamePlayers, Cards, Pots, BettingRounds)
- Does NOT expose mutation methods — that's the coordinator's job

**A3. IGameExecutionCoordinator / GameExecutionCoordinator** — `src/CardGames.Poker.Api/Services/InMemoryEngine/GameExecutionCoordinator.cs`
- Singleton wrapping `ConcurrentDictionary<Guid, SemaphoreSlim>` for per-game locks
- `ExecuteAsync<T>(Guid gameId, Func<ActiveGameRuntimeState, CancellationToken, Task<T>> action, CancellationToken ct)`
- Acquires per-game semaphore, loads game into GameStateManager if not present, executes action, releases
- All command handlers call through this

**A4. GameStateCheckpointService** — `src/CardGames.Poker.Api/Services/InMemoryEngine/GameStateCheckpointService.cs`
- `IGameStateCheckpointService` with `Task CheckpointAsync(ActiveGameRuntimeState state, CancellationToken ct)` 
- Writes current runtime state back to DB via `CardsDbContext`: Game, GamePlayers, Cards, Pots, BettingRounds
- Called on phase transitions and hand completion
- Uses a fresh scoped DbContext (from `IServiceScopeFactory`)

**A5. GameStateHydrator** — `src/CardGames.Poker.Api/Services/InMemoryEngine/GameStateHydrator.cs`
- `Task<ActiveGameRuntimeState> HydrateFromDatabaseAsync(Guid gameId, CancellationToken ct)`
- Single comprehensive query with Includes to load full game state
- Maps EF entities → runtime records (detached)
- Used by GameStateManager.LoadGameAsync and crash recovery

**A6. InMemoryEngineOptions** — `src/CardGames.Poker.Api/Services/InMemoryEngine/InMemoryEngineOptions.cs`
- Kill switch: `bool Enabled` (default false)
- `TimeSpan CheckpointInterval` (for background periodic checkpoint)
- `TimeSpan IdleEvictionAfter` (for cleanup)

**A7. DI registration** — Update `Program.cs`
- Register GameStateManager, GameExecutionCoordinator, GameStateCheckpointService, GameStateHydrator as singletons
- Bind InMemoryEngineOptions from config

---

### Phase B: Handler Migration Pattern (parallel with Phase C for different game types)

**B1. Define the handler migration pattern**
- Each handler's `Handle` method changes from:
  1. Load from CardsDbContext (Include chains)
  2. Validate + mutate EF entities
  3. SaveChangesAsync
- To:
  1. `_coordinator.ExecuteAsync(gameId, async (gameState, ct) => { ... })`
  2. Validate + mutate runtime state in-place (under lock)
  3. If phase transition: `_checkpointService.CheckpointAsync(gameState, ct)`
  4. Return result
- The GameStateBroadcastingBehavior continues to auto-broadcast after success

**B2. Create base helper: RuntimeStateExtensions** — `src/CardGames.Poker.Api/Services/InMemoryEngine/RuntimeStateExtensions.cs`
- Extension methods for common operations on `ActiveGameRuntimeState`:
  - `GetActivePlayer(seatIndex)`, `GetCurrentActor()`, `GetPlayersInHand()`
  - `AddBettingAction(...)`, `AdvanceBettingRound(...)`, `CreatePot(...)`, `DealCards(...)`
  - `GetPlayerCards(playerId)`, `GetCommunityCards()`
  - `IncrementVersion()` — bumps Version counter

**B3. Update ITableStateBuilder.BuildFullStateAsync** 
- Add overload or path: `BuildFullStateAsync(ActiveGameRuntimeState state)` that builds DTOs from runtime state instead of DB
- This enables broadcasting from in-memory state

---

### Phase C: Migrate All Handlers (by game type — can parallelize across types)

Order of migration (by complexity, simplest first):

**C1. Common handlers** (9 handlers)
- CreateGameCommandHandler — still uses DB (creates game row), but also initializes GameStateManager entry
- JoinGameCommandHandler — DB for seat assignment, then update runtime state
- LeaveGameCommandHandler — update runtime state, checkpoint
- AddChipsCommandHandler — update runtime player chipstack
- DeleteGameCommandHandler — evict from GameStateManager
- UpdateTableSettingsCommandHandler, ToggleSitOutCommandHandler, etc.

**C2. Generic routed handlers** (2)
- StartHandCommandHandler — delegates to IGameFlowHandler, operates on runtime state
- PerformShowdownCommandHandler — evaluates hands from runtime cards, settlements synchronous to DB

**C3. HoldEm** (3 handlers) — good reference implementation
- StartHandCommandHandler
- ProcessBettingActionCommandHandler
- PerformShowdownCommandHandler

**C4. FiveCardDraw** (6 handlers) — adds draw mechanics
- StartHand, DealHands, CollectAntes, ProcessBettingAction, ProcessDraw, PerformShowdown

**C5. SevenCardStud** (6 handlers) — stud-specific dealing
**C6. Baseball** (6 handlers) — unique BuyCard mechanic
**C7. FollowTheQueen** (6 handlers)
**C8. GoodBadUgly** (5 handlers)
**C9. KingsAndLows** (8 handlers) — most complex, DropOrStay + IMediator chaining
**C10. TwosJacksManWithTheAxe** (6 handlers)
**C11. PairPressure** (5 handlers)
**C12. Tollbooth** (6 handlers) — unique ChooseCard mechanic
**C13. HoldTheBaseball** (4 handlers)
**C14. IrishHoldEm** (ProcessDiscard, FoldDuringDraw)
**C15. ScrewYourNeighbor** (KeepOrTrade)
**C16. InBetween** (AceChoice, PlaceBet)
**C17. BobBarker** (SelectShowcase)

---

### Phase D: Background Services & Broadcast Integration

**D1. Update ContinuousPlayBackgroundService**
- Read from GameStateManager instead of DB for active game discovery
- Still fall back to DB query on cache empty (cold start)
- Phase transitions on runtime state, then checkpoint

**D2. Update GameStateBroadcaster**
- `BroadcastGameStateAsync` builds DTOs from runtime state (via ITableStateBuilder overload)
- Cache snapshot in ActiveGameCache (existing) for reconnects
- Still send via SignalR

**D3. Update GameHub.SendStateSnapshotToCallerAsync**
- On reconnect: try ActiveGameCache first, then GameStateManager, then DB fallback

**D4. ActiveGameCacheEvictionService**
- Also evict GameStateManager entries for truly stale/completed games

---

### Phase E: Checkpoint & Recovery

**E1. Background checkpoint service** — `GameStatePeriodicCheckpointService.cs`
- BackgroundService that periodically checkpoints all dirty game states (every 30 sec configurable)
- Iterates GameStateManager, writes dirty states to DB

**E2. Startup recovery** — In `GameStateManager` initialization
- On app startup, query DB for all games with Status = InProgress or BetweenHands
- Hydrate each into runtime state
- Log recovered game count

**E3. Phase-transition checkpoints**
- Already in handler pattern (Phase B): after each phase change, call CheckpointAsync

---

### Phase F: Tests

**F1. Unit tests for ActiveGameRuntimeState** — validate state transitions
**F2. Unit tests for GameStateManager** — load/get/remove/count
**F3. Unit tests for GameExecutionCoordinator** — lock semantics, concurrent access
**F4. Unit tests for GameStateHydrator** — entity → runtime mapping
**F5. Unit tests for RuntimeStateExtensions** — betting, dealing, phase advance
**F6. Integration test: full hand lifecycle** — deal → bet → showdown from runtime state
**F7. Test: checkpoint writes correct DB state
**F8. Test: recovery loads correct runtime state from DB

---

## Relevant Files

### New files to create:
- `src/CardGames.Poker.Api/Services/InMemoryEngine/ActiveGameRuntimeState.cs` — runtime model
- `src/CardGames.Poker.Api/Services/InMemoryEngine/RuntimeGamePlayer.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/RuntimePot.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/RuntimeCard.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/RuntimeBettingRound.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/RuntimeBettingAction.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/IGameStateManager.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/GameStateManager.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/IGameExecutionCoordinator.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/GameExecutionCoordinator.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/IGameStateCheckpointService.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/GameStateCheckpointService.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/IGameStateHydrator.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/GameStateHydrator.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/InMemoryEngineOptions.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/RuntimeStateExtensions.cs`
- `src/CardGames.Poker.Api/Services/InMemoryEngine/GameStatePeriodicCheckpointService.cs`
- `src/Tests/CardGames.Poker.Tests/Api/Services/InMemoryEngine/` — test files

### Existing files to modify:
- `src/CardGames.Poker.Api/Program.cs` — DI registration
- `src/CardGames.Poker.Api/Services/GameStateBroadcaster.cs` — build from runtime state
- `src/CardGames.Poker.Api/Services/TableStateBuilder.cs` — add runtime-state overload
- `src/CardGames.Poker.Api/Hubs/GameHub.cs` — add GameStateManager fallback
- `src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs` — read from GameStateManager
- ALL command handler files (~100) across Features/Games/*/v1/Commands/

### Reference files (architecture, not modified):
- `src/CardGames.Poker.Api/Data/Entities/Game.cs` — entity structure for hydration mapping
- `src/CardGames.Poker.Api/Data/Entities/GamePlayer.cs`
- `src/CardGames.Poker.Api/Data/Entities/GameCard.cs`
- `src/CardGames.Poker.Api/Data/Entities/Pot.cs`
- `src/CardGames.Poker.Api/Data/Entities/BettingRound.cs`
- `src/CardGames.Poker.Api/Data/Entities/BettingActionRecord.cs`
- `src/CardGames.Poker.Api/GameFlow/IGameFlowHandler.cs` — flow handler interface
- `src/CardGames.Poker.Api/GameFlow/GameFlowHandlerFactory.cs`
- `src/CardGames.Poker.Api/Services/Cache/ActiveGameCache.cs` — existing Phase 1 cache
- `src/CardGames.Poker.Api/Services/Cache/IActiveGameCache.cs`
- `src/CardGames.Poker.Api/Infrastructure/PipelineBehaviors/GameStateBroadcastingBehavior.cs`

## Verification
1. `dotnet build src/CardGames.sln` — no build errors after each phase
2. `dotnet test src/CardGames.sln` — all existing tests pass
3. New unit tests for all infrastructure (GameStateManager, Coordinator, Hydrator, Checkpoint)
4. Integration test: full hand lifecycle without DB reads during play
5. Verify checkpoint writes produce correct DB state
6. Verify recovery loads correct runtime state from DB
7. Verify kill switch (`InMemoryEngineOptions.Enabled = false`) falls back to DB path

## Scope Boundaries
- IN: GameStateManager, coordinator, checkpoint, hydrator, all handler migration, background service updates, broadcast integration, tests
- OUT: Idempotency keys (not needed for checkpoint-only recovery), Redis backplane, action log replay, delta-based broadcasting (keep full snapshots)
