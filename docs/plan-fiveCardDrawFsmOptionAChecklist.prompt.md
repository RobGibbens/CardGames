## Plan (DRAFT): Five Card Draw FSM Orchestrator — Option A Checklist

Centralize Five Card Draw flow changes behind a single orchestrator (pure trigger → transition → emitted commands), while keeping the existing schedulers (continuous-play poller + in-memory timer) as signal sources only. Option A means: the poller still finds “next hand due” games, but instead of mutating EF state in-place it calls the orchestrator with a deterministic trigger. For the pilot we’ll rely on existing MediatR + [src/CardGames.Poker.Api/Infrastructure/PipelineBehaviors/GameStateBroadcastingBehavior.cs](src/CardGames.Poker.Api/Infrastructure/PipelineBehaviors/GameStateBroadcastingBehavior.cs) for broadcasting, and we’ll persist FSM state in a new table keyed by GameId.

**Concrete types (pilot scope)**
- Triggers (domain-level, pure inputs)
  - FiveCardDrawFlowTrigger.NextHandDue
  - FiveCardDrawFlowTrigger.ActionTimerExpired(seatIndex)
- Emitted commands (infra-level, executed by orchestrator)
  - StartNextHand (finalize leaves → apply pending chips → eligibility checks → reset per-hand state → collect antes → deal → set phase/actor indices)
  - PerformAutoAction(seatIndex) (stand pat / check/fold style, matching today’s behavior)
- Orchestrator surface
  - IGameFlowOrchestrator.FireAsync(gameId, trigger, nowUtc, cancellationToken)

**Steps**
1. Add persistence table for FSM snapshot (pilot-ready, minimal schema)
   - Add a new EF entity + migration in [src/CardGames.Poker.Api/Data](src/CardGames.Poker.Api/Data) (same project that owns CardsDbContext).
   - Table idea: GameFlowSnapshot
     - GameId (PK/FK to Game)
     - Engine (string; e.g., FiveCardDrawFsmV1)
     - SnapshotJson (string)
     - UpdatedAt (timestamp) and optional RowVersion for concurrency
   - Gating rule for pilot: “FSM enabled” if a snapshot row exists for the game + game type is Five Card Draw.

2. Define the Five Card Draw flow “trigger → transition → commands” as a pure unit-testable module
   - Location: keep pure logic in the non-API project (e.g., under [src/CardGames.Poker/Games/FiveCardDraw](src/CardGames.Poker/Games/FiveCardDraw)) so it’s deterministic and testable without EF/SignalR.
   - Input state should be minimal and derived from persisted game state + snapshot (e.g., current phase, hand number, relevant timestamps, whether eligible players >= 2).
   - Output should be: next snapshot + a list of emitted commands (the orchestrator will map these to real actions).

3. Implement the API orchestrator that owns “load → fire → save → execute”
   - Add an API service (suggest under [src/CardGames.Poker.Api/GameFlow](src/CardGames.Poker.Api/GameFlow)) that:
     - Loads Game and its related data (at least GameType + GamePlayers) and loads GameFlowSnapshot.
     - Builds the FSM input state + fires the trigger into the pure module.
     - Persists updated snapshot (and any required direct game field updates if you decide the orchestrator owns those).
     - Executes emitted commands (pilot-friendly approach: call existing MediatR commands that already mutate the DB correctly).
   - Broadcasting decision: rely on the existing MediatR pipeline broadcasting via IGameStateChangingCommand.

4. Option A change (core): reroute “next hand due” from background mutator to orchestrator trigger
   - Update [src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs](src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs):
     - In ProcessGamesReadyForNextHandAsync, replace the per-game call to StartNextHandAsync at [ContinuousPlayBackgroundService.cs](src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs#L95-L104) with orchestrator.FireAsync(game.Id, NextHandDue, nowUtc).
     - Leave the selection query intact initially at [ContinuousPlayBackgroundService.cs](src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs#L85-L94).
   - Constraint: do not start a hand without enough players. The orchestrator must re-check eligibility and either:
     - Start the hand if eligible players >= 2, or
     - Transition to/stay in WaitingForPlayers and clear NextHandStartsAt (matching today’s intent).

5. Reroute action timer expiry from flow handler auto-action to orchestrator trigger
   - Update [src/CardGames.Poker.Api/Services/AutoActionService.cs](src/CardGames.Poker.Api/Services/AutoActionService.cs):
     - In PerformAutoActionAsync(gameId, seatIndex), if FSM-enabled Five Card Draw: call orchestrator.FireAsync(gameId, ActionTimerExpired(seatIndex), nowUtc) and return.
     - Else: keep current path IGameFlowHandler.PerformAutoActionAsync unchanged for non-pilot games.
   - Timer callback wiring stays where it is (still created by [src/CardGames.Poker.Api/Services/GameStateBroadcaster.cs](src/CardGames.Poker.Api/Services/GameStateBroadcaster.cs)), but expiry becomes deterministic via orchestrator decisions.

6. Decide the “start next hand” execution strategy (pick one for pilot)
   - A) Command reuse approach: orchestrator emits commands that map onto existing MediatR handlers (least code churn).
     - Caveat: Five Card Draw’s existing StartHandCommandHandler likely doesn’t accept WaitingForPlayers as a valid phase. Since we can’t start without enough players anyway, either:
       - Avoid calling StartHandCommand from WaitingForPlayers cases (orchestrator just clears NextHandStartsAt + sets phase), or
       - Introduce a dedicated “StartNextHandIfEligible” MediatR command used only by the orchestrator.
   - B) Direct EF runner approach: orchestrator executes the existing StartNextHandAsync logic but moved out of the background service into a command runner (single-owner, but more refactor).

7. Keep non-FiveCardDraw behavior stable during pilot
   - Leave DrawComplete and chip-check pause logic in [src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs](src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs) untouched for now; Five Card Draw doesn’t use DrawComplete, and chip-check is inactive for it.
   - Optionally leave ProcessAbandonedGamesAsync as-is initially, or add a follow-up trigger later (GameAbandonedDetected) once the pilot is stable.

8. Add tests focused on the new FSM behavior
   - Unit tests for the pure Five Card Draw flow module: triggers produce expected emitted commands and next snapshot.
   - One integration test that proves Option A wiring: set NextHandStartsAt in DB for an FSM-enabled Five Card Draw game → background service tick calls orchestrator → orchestrator sends MediatR command(s) → resulting phase/hand changes occur.

**Verification**
- Run dotnet tests for affected projects (start with the new FSM unit tests).
- Manual smoke:
  - Finish a Five Card Draw hand → confirm NextHandStartsAt is set.
  - Wait until due → confirm the poller advances via orchestrator (broadcast still via MediatR pipeline).
  - Let a player timer expire → confirm it routes through orchestrator and results match today’s auto-action.

**Decisions captured**
- Option A: background poller fires orchestrator triggers (does not mutate state directly).
- Broadcasting: rely on MediatR pipeline behavior for IGameStateChangingCommand.
- Persistence: store flow snapshot in a new table keyed by GameId.
- Constraint: never start a hand without enough players (orchestrator must enforce eligibility checks).
