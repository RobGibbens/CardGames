# Fix plan: stud tables stall when the action timer expires (no auto-fold)

**Audit finding:** “Table stalls: the timer never auto-folds in six stud games.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** Server-side action timer expiration → `AutoActionService` → `IGameFlowHandler` → MediatR betting commands for **street-based stud-family** game types. Do not change timer duration, client UI, or human betting HTTP routes unless tests require it.

---

## 1. Problem summary

On **Seven Card Stud–family** tables (`ThirdStreet` … `SeventhStreet`), the UI shows an action timer and counts down. When it reaches zero, the table **does not advance**: the current player remains on the clock (or the hand appears frozen), no fold/check is recorded, and no broadcast advances the actor.

**Human-initiated** betting on the same table **works** (fold/call/raise via the game-specific API). Only **timer expiry** is broken.

### Affected game type codes (the “six stud games”)

These codes use street phases and dedicated `ProcessBettingAction` handlers, but their flow handlers **do not** override `SendBettingActionAsync` (except where noted):

| Code | Flow handler | Live betting MediatR command | Timer auto-action today |
|------|----------------|------------------------------|-------------------------|
| `SEVENCARDSTUD` | `SevenCardStudFlowHandler` | `SevenCardStud.v1.Commands.ProcessBettingAction` | **Five Card Draw** command (wrong) |
| `RAZZ` | `RazzFlowHandler` : `SevenCardStudFlowHandler` | Same Seven Card Stud endpoint/handler (no separate Razz betting feature) | **Inherited wrong** |
| `FOLLOWTHEQUEEN` | `FollowTheQueenFlowHandler` | `FollowTheQueen.v1.Commands.ProcessBettingAction` | **FCD** command (wrong) |
| `PAIRPRESSURE` | `PairPressureFlowHandler` | `PairPressure.v1` → delegates to **Seven Card Stud** inner handler | **FCD** command (wrong) |
| `BASEBALL` | `BaseballFlowHandler` | `Baseball.v1.Commands.ProcessBettingAction` | **FCD** command (wrong) |
| `GOODBADUGLY` | `GoodBadUglyFlowHandler` | `GoodBadUgly.v1.Commands.ProcessBettingAction` | **FCD** command (wrong) |

**Reference (already correct):** `TOLLBOOTH` — `TollboothFlowHandler` overrides `SendBettingActionAsync` (~353–366) to send `Tollbooth.v1.Commands.ProcessBettingAction`. Use this as the pattern.

**Not in scope:** `HOLDTHEBASEBALL` (Hold’em-style phases, already has Hold’em-style overrides elsewhere in the family).

---

## 2. Root cause (exact mechanism)

### 2.1 Timer **does** start on stud streets (not the primary bug)

`GameStateBroadcaster.ManageActionTimer` (`src/CardGames.Poker.Api/Services/GameStateBroadcaster.cs` ~121–123) starts a timer when:

```csharp
var requiresAction = ActionPhases.Contains(state.CurrentPhase) ||
                     state.CurrentPhaseRequiresAction;
```

`ActionPhases` (~25–31) lists only draw/Hold’em-style names (`FirstBettingRound`, `SecondBettingRound`, `DrawPhase`, `DropOrStay`) — **not** `ThirdStreet` … `SeventhStreet`.

Stud streets still qualify because `TableStateBuilder` (~187–191, ~266) sets `CurrentPhaseRequiresAction` from `PokerGameRulesRegistry` + phase descriptor. For example `SevenCardStudRules` marks `ThirdStreet` … `SeventhStreet` with `RequiresPlayerAction = true` (`src/CardGames.Poker/Games/SevenCardStud/SevenCardStudRules.cs`).

So logs should show **“Starting action timer”** and **“Timer expired … performing auto-action”** for stud. If those lines are missing, investigate `CurrentActorSeatIndex < 0`, `IsPaused`, or chip-check pause — but the audited stall happens **after** expiry with auto-action invoked.

### 2.2 Auto-action uses the **wrong** MediatR betting command

**Entry:** `ActionTimerService` callback → `AutoActionService.PerformAutoActionAsync` (`AutoActionService.cs` ~29–74) → `IGameFlowHandlerFactory.GetHandler(gameTypeCode)` → `PerformAutoActionAsync`.

**Betting path:** `BaseGameFlowHandler.PerformAutoActionAsync` (~120–126) calls `PerformAutoBettingActionAsync` when `IsBettingPhase(context.CurrentPhase)` (rules category `"Betting"`) and `context.PlayerSeatIndex >= 0`.

**Check/fold decision:** `PerformAutoBettingActionAsync` (~138–182) computes Check vs Fold from DB state, then calls `SendBettingActionAsync`.

**Default command (bug):** `BaseGameFlowHandler.SendBettingActionAsync` (~189–203):

```csharp
// Default: Use FiveCardDraw command (legacy behavior)
// Games that use different commands MUST override this method
var command = new ProcessBettingActionCommand(context.GameId, action, amount);
await context.Mediator.Send(command, context.CancellationToken);
```

The unqualified `ProcessBettingActionCommand` is **`CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand`**.

**FCD handler rejects stud phases** (`FiveCardDraw/.../ProcessBettingActionCommandHandler.cs` ~46–59):

- Valid phases: `FirstBettingRound`, `SecondBettingRound` only.
- Game in `ThirdStreet` (etc.) → `ProcessBettingActionError` with `InvalidGameState`, message like *“Betting is only allowed during FirstBettingRound or SecondBettingRound”*.

**Stud handler accepts stud streets** (`SevenCardStud/.../ProcessBettingActionCommandHandler.cs` ~81–98): `ThirdStreet` … `SeventhStreet` (and `Showdown` in that handler).

### 2.3 Failure is **silent** (why the table “stalls”)

`SendBettingActionAsync` only catches **`Exception`** (~195–202). MediatR returns `OneOf<Success, Error>` **without throwing** on business errors. The FCD handler returns `ProcessBettingActionError`; **no exception**, **no log**, **no state change**, timer may already be stopped on the next broadcast — user sees a stuck hand.

### 2.4 Human path vs timer path (why manual play works)

Players use game-specific HTTP endpoints / client wrappers that send the **correct** command type. Example: Pair Pressure endpoint sends `PairPressure.ProcessBettingActionCommand`, which forwards to the Seven Card Stud handler (`PairPressure/.../ProcessBettingActionCommandHandler.cs`).

Timer path never hits those endpoints; it only goes through flow handler `SendBettingActionAsync`.

### Architecture diagram

```
GameStateBroadcaster.ManageActionTimer
  → ActionTimerService.StartTimer(onExpired)
       → AutoActionService.PerformAutoActionAsync(gameId, seatIndex)
            → SevenCardStudFlowHandler (etc.)
                 → PerformAutoBettingActionAsync  (check/fold OK)
                      → SendBettingActionAsync
                           → ❌ FiveCardDraw.ProcessBettingActionCommand
                                → InvalidGameState (ThirdStreet)
                                     → OneOf error, no throw → STALL
```

**Correct path after fix:**

```
SendBettingActionAsync
  → SevenCardStud.ProcessBettingActionCommand  (or game-specific equivalent)
       → fold/check applied, IGameStateChangingCommand → broadcast → hand advances
```

---

## 3. Intended behavior (spec)

1. When the action timer expires on a stud street (`ThirdStreet` … `SeventhStreet`) for game types in §1, the server performs the same **Check if legal, else Fold** policy as other games (`PerformAutoBettingActionAsync`).
2. The auto-action must invoke the **same MediatR command type** that the human HTTP API uses for that `GameType.Code`.
3. A successful command must mutate DB state and trigger the normal post-command broadcast (existing `IGameStateChangingCommand` pipeline).
4. If the command returns `ProcessBettingActionError`, log at **Warning** with `Code` and `Message` (recommended hardening in §5.3).
5. **Seat authority:** Implement together with or immediately after `docs/Auditing/fix-timer-wrong-seat-auto-action.md` so a stale timer cannot fold the wrong seat; stud stall fix is independent of seat gate but both touch timer betting.

---

## 4. Implementation plan

### 4.1 `SevenCardStudFlowHandler` — fixes `SEVENCARDSTUD` and `RAZZ`

**File:** `src/CardGames.Poker.Api/GameFlow/SevenCardStudFlowHandler.cs`

Add `using` for:

- `CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction`
- `CardGames.Poker.Api.Data.Entities` (if `BettingActionType` needed — match `HoldEmFlowHandler` style)

Override `SendBettingActionAsync` mirroring `HoldEmFlowHandler.cs` (~48–59):

```csharp
protected override async Task SendBettingActionAsync(
    AutoActionContext context,
    BettingActionType action,
    int amount = 0)
{
    var command = new ProcessBettingActionCommand(context.GameId, action, amount);
    try
    {
        await context.Mediator.Send(command, context.CancellationToken);
    }
    catch (Exception ex)
    {
        context.Logger.LogError(
            ex,
            "Error performing auto-betting action for Seven Card Stud game {GameId}",
            context.GameId);
    }
}
```

`RazzFlowHandler` inherits this; **no separate change** unless you prefer an explicit override for log message clarity.

### 4.2 `FollowTheQueenFlowHandler`

**File:** `src/CardGames.Poker.Api/GameFlow/FollowTheQueenFlowHandler.cs`

Override `SendBettingActionAsync` to send:

`Features.Games.FollowTheQueen.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand`

Same try/catch pattern as Hold’em.

### 4.3 `PairPressureFlowHandler`

**File:** `src/CardGames.Poker.Api/GameFlow/PairPressureFlowHandler.cs`

Override to send:

`Features.Games.PairPressure.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand`

(Do **not** send the Seven Card Stud command type directly unless you confirm MediatR registration — the Pair Pressure wrapper type is what the API uses.)

### 4.4 `BaseballFlowHandler`

**File:** `src/CardGames.Poker.Api/GameFlow/BaseballFlowHandler.cs`

Override to send:

`Features.Games.Baseball.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand`

### 4.5 `GoodBadUglyFlowHandler`

**File:** `src/CardGames.Poker.Api/GameFlow/GoodBadUglyFlowHandler.cs`

Override to send:

`Features.Games.GoodBadUgly.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand`

**Note:** Seven Card Stud’s shared handler **rejects** `GOODBADUGLY` explicitly (~58–65 in `SevenCardStud/.../ProcessBettingActionCommandHandler.cs`). Auto-action must **not** use the Seven Card Stud command for this game type.

### 4.6 Do not change

- `TollboothFlowHandler` — already correct.
- `BaseGameFlowHandler.SendBettingActionAsync` default — remains FCD for true draw games (`FiveCardDrawFlowHandler` does not override).
- Human-facing endpoints.

### 4.7 Optional refactor (only if you want less duplication)

Extract a small protected helper on `BaseGameFlowHandler`:

```csharp
protected async Task SendBettingCommandAsync<TCommand>(
    AutoActionContext context,
    TCommand command,
    string gameLabelForLogs)
```

Not required for the minimal fix; six overrides are acceptable and match existing Hold’em/Tollbooth style.

---

## 5. Recommended hardening (same PR or follow-up)

### 5.1 Log `OneOf` errors from auto betting

After `await context.Mediator.Send(...)`, if the result is a known error type, log Warning. Options:

- Per-handler duplicate switch (heavy), or
- Shared extension for `OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>` used in each override, or
- Enhance `SendBettingCommandAsync` to inspect `OneOf` via reflection (avoid unless necessary).

Minimum: in **one** stud integration test, assert command **type** is correct; in manual QA, confirm Warning logs if you inject invalid state.

### 5.2 `ActionPhases` explicit stud streets (optional)

Add to `GameStateBroadcaster.ActionPhases`:

`ThirdStreet`, `FourthStreet`, `FifthStreet`, `SixthStreet`, `SeventhStreet`

**Not required** for correctness while `CurrentPhaseRequiresAction` is populated, but improves clarity and protects timer start if rules registry ever fails for a stud code. If added, extend `GameStateBroadcasterTests` with a stud phase + `CurrentPhaseRequiresAction: false` still starting timer when phase name is in set.

### 5.3 Seat gate (`fix-timer-wrong-seat-auto-action.md`)

Before `PerformAutoBettingActionAsync`, ensure `playerSeatIndex == bettingRound.CurrentActorIndex` (or resolved authoritative seat). Prevents wrong-seat auto-action once stud commands actually mutate state.

---

## 6. Verification — automated tests

### 6.1 Integration — `AutoActionServiceTests` (primary)

**File:** `src/Tests/CardGames.IntegrationTests/Services/AutoActionServiceTests.cs`

Mirror existing tests `PerformAutoActionAsync_Betting_CanCheck_Checks` / `_CannotCheck_Folds` (~137–217):

1. Create `Game` with `GameType.Code = "SEVENCARDSTUD"` (and duplicate for one other code, e.g. `FOLLOWTHEQUEEN`).
2. `CurrentPhase = "ThirdStreet"` (or `FourthStreet`).
3. Add `GamePlayer` at seat 0, active, with `CurrentBet` set so max active bet equals or exceeds player bet (check vs fold cases).
4. Add **active** `BettingRound` for current hand: `IsComplete = false`, `CurrentActorIndex = 0`, `HandNumber` matches game.
5. Call `PerformAutoActionAsync(gameId, playerSeat: 0)`.
6. Assert `_mediator.SentCommands` contains **exactly one** command of the expected type:
   - Stud: `CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand`
   - FTQ: `FollowTheQueen.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand`
7. Assert **no** `FiveCardDraw.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand` in sent commands.

Extend `FakeMediator` if it only records FCD commands today — ensure it records all command types.

### 6.2 Unit — flow handler override (optional, fast)

New file: `Tests/CardGames.Poker.Tests/GameFlow/StudAutoActionRoutingTests.cs`

- Instantiate `SevenCardStudFlowHandler`, build minimal `AutoActionContext` with fake `IMediator` that captures sent command type.
- Call `PerformAutoActionAsync` with phase `ThirdStreet` — assert Seven Card Stud command.

### 6.3 Regression — Tollbooth

If touching shared base code, run existing Tollbooth auto-action coverage (if any) or add assertion that Tollbooth still sends Tollbooth command.

### 6.4 `GameStateBroadcasterTests` (optional)

Test: `CurrentPhase = "ThirdStreet"`, `CurrentPhaseRequiresAction = true`, `CurrentActorSeatIndex = 1` → `StartTimer` called once with seat 1. Confirms timer wiring for stud (documents expected behavior from §2.1).

### Commands

```bash
cd src
dotnet test Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~AutoAction"
dotnet test Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~GameStateBroadcaster|FullyQualifiedName~StudAutoAction"
```

---

## 7. Manual QA checklist

1. **Seven Card Stud**, 2+ players, reach **Third Street** with a facing bet.
2. Let timer expire on the player who must act.
3. **Expect:** fold recorded in hand history / seat shows folded; action passes or street completes.
4. Repeat with **no bet to call** (player can check) — **expect:** check, not fold.
5. Repeat on **Fifth Street** (big bet street).
6. **Razz** — same as (1)–(4) on a street.
7. **Follow the Queen** — timer expiry folds when behind.
8. **Good Bad Ugly** — confirm auto-action does not hit Seven Card Stud error message in logs.
9. **Tollbooth** — smoke test timer still works (regression).
10. Server logs: on expiry, `Performing auto-action` → no silent return; after fix, no FCD `InvalidGameState` for stud phases.

---

## 8. Acceptance criteria

- [ ] Timer expiry on `ThirdStreet` … `SeventhStreet` for all six game codes in §1 applies check or fold via the **correct** `ProcessBettingAction` command.
- [ ] `RAZZ` fixed by `SevenCardStudFlowHandler` override (no Razz-specific betting feature).
- [ ] No `FiveCardDraw` `ProcessBettingActionCommand` sent for stud-family timer auto-actions.
- [ ] Hand state advances (actor changes, fold flag, or round complete) after successful auto-action.
- [ ] Existing non-stud `AutoActionServiceTests` remain green.
- [ ] (Recommended) `ProcessBettingActionError` from auto-action is logged, not silent.

---

## 9. File reference

| Area | Path |
|------|------|
| Timer start / callback | `Services/GameStateBroadcaster.cs` (`ManageActionTimer`, `ActionPhases`) |
| Auto-action entry | `Services/AutoActionService.cs` |
| Betting auto default (wrong command) | `GameFlow/BaseGameFlowHandler.cs` (`SendBettingActionAsync` ~189–203) |
| Stud handler (add override) | `GameFlow/SevenCardStudFlowHandler.cs` |
| Razz (inherits stud) | `GameFlow/RazzFlowHandler.cs` |
| Other stud variants | `FollowTheQueenFlowHandler.cs`, `PairPressureFlowHandler.cs`, `BaseballFlowHandler.cs`, `GoodBadUglyFlowHandler.cs` |
| Reference override | `GameFlow/TollboothFlowHandler.cs` (~353–366) |
| Pattern override | `GameFlow/HoldEmFlowHandler.cs` (~48–59) |
| FCD phase guard | `Features/Games/FiveCardDraw/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Stud phase guard | `Features/Games/SevenCardStud/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Phase metadata | `Games/SevenCardStud/SevenCardStudRules.cs`, `Services/TableStateBuilder.cs` |
| Tests | `Tests/CardGames.IntegrationTests/Services/AutoActionServiceTests.cs` |

---

## 10. Related audit items

| Doc | Relationship |
|-----|----------------|
| `fix-timer-wrong-seat-auto-action.md` | Seat gate + same missing stud `SendBettingActionAsync` override (§4.7 / acceptance criteria there) |
| `fix-razz-showdown-lowball.md` | Razz showdown; unrelated to timer betting but same game family |
| `fix-short-blind-all-in.md` | All-in players should not receive timers; reduces edge cases |
| `fix-big-blind-option.md` | Hold’em-only; listed for timer/seat race context |

---

## 11. Out of scope

- Client timer display / SignalR sync (server must mutate state correctly first).
- Changing auto-action policy (e.g. auto-call instead of fold) — keep check-if-free else fold.
- In-memory `ActionTimerService` multi-instance deployment.
- Adding new game types without `SendBettingActionAsync` override (document in `docs/ADDING_NEW_GAMES.md` when fixing).

---

## 12. Implementation order (suggested for LLM)

1. Add `SevenCardStudFlowHandler.SendBettingActionAsync` override + integration test for `SEVENCARDSTUD` / `ThirdStreet`.
2. Add overrides for `FOLLOWTHEQUEEN`, `PAIRPRESSURE`, `BASEBALL`, `GOODBADUGLY` + at least one extra test for a non-stud-command type (e.g. FTQ).
3. Run full `AutoAction` test filter.
4. (Optional) OneOf error logging in overrides or shared helper.
5. (Optional) Seat resolver from `fix-timer-wrong-seat-auto-action.md`.
6. Manual QA §7 on a local table.
