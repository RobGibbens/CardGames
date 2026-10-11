# Fix plan: timer auto-action must only act for the current actor seat

**Audit finding:** “A timer auto-action can hit the wrong seat.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** Server-side action timer expiration → `AutoActionService` → flow handler → MediatR commands. Align timer seat with authoritative “who must act” state in the database.

---

## 1. Problem summary

When a player’s turn timer expires, the API calls `IAutoActionService.PerformAutoActionAsync(gameId, playerSeatIndex)` with the **seat index captured when the timer started**. The auto-action pipeline **does not verify** that this seat is still the player who must act. It then sends commands that, for most betting paths, apply to **`BettingRound.CurrentActorIndex`**, not to the timer seat.

That combination produces **two failure modes**:

| Mode | What happens |
|------|----------------|
| **A. Stale timer** | Player A’s timer fires after action already advanced to B → auto-action runs while B is current actor → **B is folded/checked** (not A). |
| **B. Mismatched logic** | `PerformAutoBettingActionAsync` computes check/fold using **timer seat**’s `CurrentBet`, but `ProcessBettingActionCommand` mutates **current actor** → wrong action type for the player who actually acted. |

### Symptom (user-visible)

- UI showed timer on seat 3; seat 4 suddenly auto-folds or auto-checks.
- Hand history shows an action for a player who was not “on the clock.”
- Rare race right after a fast human action (timer callback in flight while state already advanced).

### Architecture (current flow)

```
GameStateBroadcaster.ManageActionTimer
  → ActionTimerService.StartTimer(gameId, seatIndex, onExpired)
       → on expiry: AutoActionService.PerformAutoActionAsync(gameId, seatIndex)
            → IGameFlowHandler.PerformAutoActionAsync(AutoActionContext)
                 → PerformAutoBettingActionAsync / draw / game-specific
                      → MediatR ProcessBettingActionCommand (no player id)
```

**Timer start:** `GameStateBroadcaster.cs` ~173–184 — `playerSeatIndex` from `TableStatePublicDto.CurrentActorSeatIndex` (backed by `game.CurrentPlayerIndex` in `TableStateBuilder` ~252).

**No seat check:** `AutoActionService.cs` ~29–74 — loads game `AsNoTracking`, builds context with timer `playerSeatIndex`, calls handler.

**Betting applies to DB actor, not timer seat:** `HoldEm/.../ProcessBettingActionCommandHandler.cs` ~124:

```csharp
var currentPlayer = activePlayers.FirstOrDefault(gp => gp.SeatPosition == bettingRound.CurrentActorIndex);
```

**Auto-action ignores actor for command target:** `BaseGameFlowHandler.PerformAutoBettingActionAsync` (~138–182) — resolves `gamePlayer` by `context.PlayerSeatIndex` for can-check only; `SendBettingActionAsync` sends `ProcessBettingActionCommand(gameId, action)` with **no seat**.

**Default betting command (wrong for many games):** `BaseGameFlowHandler.SendBettingActionAsync` (~189–193) uses **Five Card Draw** `ProcessBettingActionCommand` unless overridden. **Seven Card Stud / Razz** do **not** override → timer betting may hit the **wrong handler** as well as the wrong seat (related defect; fix routing in same PR or immediately after).

---

## 2. Intended behavior (spec)

1. **Authoritative actor:** At auto-action time, reload game + active betting round (or draw index) from the database **with tracking** if the handler will mutate state.
2. **Seat match (single-actor phases):** If `playerSeatIndex >= 0`, it **must equal** the authoritative seat for that phase. If not, **no-op** (log at Information/Warning); do not send MediatR commands.
3. **Apply action to matched player:** For betting, after a successful match, check/fold must be computed for **that same** player (the current actor).
4. **Global timer (`playerSeatIndex == -1`):** Used for simultaneous phases (`DropOrStay`, Bob Barker `DrawPhase` showcase). Skip seat equality check; keep existing handler behavior (`KingsAndLowsFlowHandler`, `BobBarkerFlowHandler`).
5. **Draw phase:** Authoritative seat is `game.CurrentDrawPlayerIndex` (see `ProcessDrawCommandHandler` ~74). Auto draw / `FoldDuringDrawCommand` must only run when timer seat matches (Irish / Crazy Pineapple fold-on-timeout).
6. **Timer lifecycle:** Starting a timer for a new actor must **stop** the previous timer (`ActionTimerService.StartTimer` already calls `StopTimer` ~38). Expired callbacks that fail the seat check must **not** change game state.
7. **Optional hardening:** After a successful human action, broadcast path already calls `ManageActionTimer`; no change required if (2) is enforced.

---

## 3. Authoritative seat resolution (implement once)

Add a small internal helper used by `AutoActionService` (or `BaseGameFlowHandler` before any auto command):

**Suggested path:** `src/CardGames.Poker.Api/Services/AutoActionSeatResolver.cs`

```csharp
internal static async Task<AutoActionTarget?> ResolveAsync(
    CardsDbContext context,
    Guid gameId,
    string phase,
    CancellationToken ct);
```

**`AutoActionTarget` record:**

| Field | Meaning |
|-------|---------|
| `Kind` | `Betting`, `Draw`, `Simultaneous`, `None` |
| `SeatIndex` | Required actor seat, or `-1` for simultaneous / none |
| `BettingRoundId` | Optional, for logging |

**Rules:**

| Phase family | Authoritative seat source |
|--------------|---------------------------|
| Hold’em pre/flop/turn/river, stud streets, draw betting rounds | Latest **incomplete** `BettingRound` for `game.CurrentHandNumber`: `CurrentActorIndex` |
| `DrawPhase` (and variants) | `game.CurrentDrawPlayerIndex` if `>= 0` |
| `DropOrStay`, Bob Barker draw (simultaneous) | `Kind = Simultaneous`, `SeatIndex = -1` |
| Phases with no player action | `Kind = None` → auto-action no-op |

Use the same phase sets as `BaseGameFlowHandler.IsBettingPhase` / `IsDrawingPhase` (or share constants).

**Sync check:** `game.CurrentPlayerIndex` should match `BettingRound.CurrentActorIndex` during betting (Hold’em handler sets both ~392–393). Prefer **`BettingRound.CurrentActorIndex`** for betting; use `CurrentPlayerIndex` only if no active betting round exists (log warning).

---

## 4. Recommended fix

### 4.1 Gate in `AutoActionService` (primary — covers all game types)

**File:** `src/CardGames.Poker.Api/Services/AutoActionService.cs`

After loading the game (switch to **tracked** load or re-query):

```csharp
var target = await AutoActionSeatResolver.ResolveAsync(context, gameId, game.CurrentPhase, ct);

if (target.Kind == AutoActionTargetKind.None)
{
    _logger.LogInformation("Skipping auto-action for game {GameId}: phase {Phase} requires no actor", gameId, game.CurrentPhase);
    return;
}

if (target.Kind != AutoActionTargetKind.Simultaneous && playerSeatIndex >= 0 && playerSeatIndex != target.SeatIndex)
{
    _logger.LogWarning(
        "Skipping stale auto-action for game {GameId}: timer seat {TimerSeat} but current actor is {ActorSeat} (phase {Phase})",
        gameId, playerSeatIndex, target.SeatIndex, game.CurrentPhase);
    return;
}

// For simultaneous, force context seat to -1
var effectiveSeat = target.Kind == AutoActionTargetKind.Simultaneous ? -1 : target.SeatIndex;
```

Pass `effectiveSeat` into `AutoActionContext` (not the raw timer seat when mismatched).

### 4.2 Harden `PerformAutoBettingActionAsync` (defense in depth)

**File:** `src/CardGames.Poker.Api/GameFlow/BaseGameFlowHandler.cs`

Before `SendBettingActionAsync`:

- Re-resolve active `BettingRound` and `CurrentActorIndex`.
- If `context.PlayerSeatIndex != CurrentActorIndex`, return.
- Compute `canCheck` using the **current actor’s** `GamePlayer`, not a stale collection on `context.Game`.

### 4.3 Draw / fold-on-timeout commands

**Files:** `FoldDuringDrawCommandHandler` (Irish Hold’em), `PerformAutoBettingActionAsync` draw branch.

Before folding or `ProcessDraw`:

- Require `command.PlayerSeatIndex == game.CurrentDrawPlayerIndex` (add to handler validation, not only auto layer).

### 4.4 Seven Card Stud / Razz routing (strongly recommended same PR)

**File:** `src/CardGames.Poker.Api/GameFlow/SevenCardStudFlowHandler.cs`

Override `SendBettingActionAsync` to send **`SevenCardStud.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand`** (mirror `HoldEmFlowHandler.cs` ~48–58).

Without this, stud timer auto-actions invoke **Five Card Draw** betting handler — wrong street logic even if seat check passes.

### 4.5 Optional: timer generation token

If races persist under load, extend `ActionTimerState` with `ulong Generation` incremented on each `StartTimer`; pass generation into callback; store `game.LastActionTimerGeneration` on broadcast (schema change). **Only if** seat gate alone is insufficient in tests.

---

## 5. What not to do (anti-patterns)

| Anti-pattern | Why |
|--------------|-----|
| Add `PlayerSeatIndex` to `ProcessBettingActionCommand` without auth | Opens impersonation; use server-side actor only + gate |
| Trust timer seat without DB reload | Stale callbacks |
| Only fix UI timer display | Server still mutates wrong seat |
| Rely on `PlayersActed` or `CurrentPlayerIndex` from `AsNoTracking` snapshot in context | Stale |

---

## 6. Implementation checklist

- [ ] Add `AutoActionSeatResolver` (+ unit tests).
- [ ] Update `AutoActionService.PerformAutoActionAsync` with gate + tracked reload.
- [ ] Update `PerformAutoBettingActionAsync` actor check + can-check for current actor.
- [ ] Add `CurrentDrawPlayerIndex` validation to `FoldDuringDrawCommandHandler`.
- [ ] Override `SevenCardStudFlowHandler.SendBettingActionAsync` (and verify Razz inherits).
- [ ] Add integration/unit tests (§7).
- [ ] Manual QA (§9).

---

## 7. Tests

### 7.1 Unit — `AutoActionSeatResolverTests`

In-memory `CardsDbContext`: game in `PreFlop`, betting round `CurrentActorIndex = 2`, timer seat `1` → resolve actor `2`.

### 7.2 Unit — stale timer no-op

**File:** extend `src/Tests/CardGames.IntegrationTests/Services/AutoActionServiceTests.cs`

**Arrange:**

- Game `PreFlop`, active betting round `CurrentActorIndex = 2`.
- Players at seats 1 and 2 with appropriate bets.
- Call `PerformAutoActionAsync(gameId, playerSeatIndex: 1)` (stale).

**Assert:**

- `_mediator.SentCommands` is **empty**.
- Warning log contains `stale` or `Skipping`.

### 7.3 Unit — current actor executes

Same setup, `PerformAutoActionAsync(gameId, 2)` with actor 2 → exactly one `ProcessBettingActionCommand` (Five Card Draw default in test harness).

### 7.4 Unit — Hold’em handler with factory

Register real `HoldEmFlowHandler`; betting round actor seat 2; auto seat 2 → `HoldEm` `ProcessBettingActionCommand` sent (type check).

### 7.5 Integration — fast action vs timer

**File:** new `TimerAutoActionSeatTests.cs` (optional, may use fake `IActionTimerService`):

1. Start hand, note actor seat A.
2. Process betting action for A via MediatR (human speed).
3. Invoke `PerformAutoActionAsync` with seat A (simulated late timer).
4. Assert **no** second action **or** no change to new actor B’s stack/fold state.

### 7.6 Regression — simultaneous timer

Existing `PerformAutoActionAsync_GlobalTimer_DropOrStay_*` and Bob Barker global tests must still pass (`seatIndex = -1`).

### Commands

```bash
cd src
dotnet test Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~AutoAction"
dotnet test Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~AutoActionSeat|FullyQualifiedName~GameStateBroadcaster"
```

---

## 8. Acceptance criteria

- [ ] Expired timer for seat **S** never applies check/fold/bet/draw to a **different** seat when a single actor is required.
- [ ] Stale timer callbacks produce **no** MediatR game mutation (log only).
- [ ] Current actor auto-action still works (check when matched, fold when behind).
- [ ] Simultaneous phases (`playerSeatIndex == -1`) unchanged.
- [ ] Stud/Razz timer betting uses **Seven Card Stud** `ProcessBettingAction` command (if routing fix included).
- [ ] No new client/API contract changes required.

---

## 9. File reference

| Area | Path |
|------|------|
| Timer start / callback | `Services/GameStateBroadcaster.cs` (`ManageActionTimer`) |
| Timer implementation | `Services/ActionTimerService.cs` (`OnTimerTickAsync` ~149–200) |
| Auto-action entry | `Services/AutoActionService.cs` |
| Betting auto default | `GameFlow/BaseGameFlowHandler.cs` (`PerformAutoBettingActionAsync`, `SendBettingActionAsync`) |
| Hold’em routing override | `GameFlow/HoldEmFlowHandler.cs` |
| Stud (missing override) | `GameFlow/SevenCardStudFlowHandler.cs`, `GameFlow/RazzFlowHandler.cs` |
| Context | `GameFlow/AutoActionContext.cs` |
| Betting actor | `Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Draw actor | `Features/Games/FiveCardDraw/v1/Commands/ProcessDraw/ProcessDrawCommandHandler.cs` |
| Fold on draw timeout | `Features/Games/IrishHoldEm/v1/Commands/FoldDuringDraw/FoldDuringDrawCommandHandler.cs` |
| Public actor in DTO | `Services/TableStateBuilder.cs` (`CurrentActorSeatIndex`) |
| Existing tests | `Tests/CardGames.IntegrationTests/Services/AutoActionServiceTests.cs` |
| Timer broadcast tests | `Tests/CardGames.Poker.Tests/Api/Services/GameStateBroadcasterTests.cs` |

---

## 10. Related audit items (cross-links)

| Doc | Relationship |
|-----|----------------|
| `fix-trusted-identity-authz.md` | Do not add client-supplied seat to betting commands without auth |
| `fix-short-blind-all-in.md` | Short blind should be `IsAllIn` so they are skipped in rotation (reduces bogus timer targets) |
| `fix-big-blind-option.md` | Wrong `playersWhoCanAct` can change actor quickly while old timer still runs |
| `fix-raise-stack-cap.md` | Hold’em handler shared by many variants auto-action routes |

---

## 11. Out of scope

- Client-side timer display sync.
- Multi-server timer affinity (in-memory `ActionTimerService` per instance).
- Changing default timer duration.
- Player identity / “only seated user can click” (separate auth work).

---

## 12. Manual QA

1. Two-player Hold’em table; enable short timer if configurable (or wait full duration).
2. Seat A on clock — have A act **before** expiry; confirm seat B never receives auto fold/check from A’s timer.
3. Let timer expire on current actor — confirm correct seat auto-acts once.
4. Three-player: confirm timer UI seat matches player who receives auto-action when time runs out.
5. (If stud fixed) Seven Card Stud street: timer expiry performs stud betting action, not draw-phase errors.
