# Fix plan: cap raises to the player’s stack

**Audit finding:** “Chips wrong: a raise is not limited to the player’s stack.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** Server-side betting handlers and shared betting logic. Do not change unrelated refactors.

---

## 1. Problem summary

Across API `ProcessBettingAction` handlers, **`Bet` is capped to `player.ChipStack`**, but **`Raise` is not**. For `Raise`, `amount` is interpreted as the player’s **total wager on the current street** (raise *to*), not the increment.

Relevant command contract (`HoldEm` command; others mirror it):

- File: `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommand.cs`
- XML: *“For raises, this is the total amount to put in, not the raise increment.”*

### 1.1 Bug A — raise above stack (chip creation)

**Example:** Hold’em preflop, round `CurrentBet = 2`, actor has `ChipStack = 100`, `CurrentBet = 0`. Client sends `Raise` with `Amount = 1000`.

- `ValidateAction` only rejects a raise when  
  `amount < CurrentBet + LastRaiseAmount` **and** `amount < ChipStack + CurrentBet`.  
  Here `1000 < 102` is false → validation passes.
- `ExecuteAction` sets `raiseChips = amount - player.CurrentBet` (1000) and subtracts from `ChipStack` → **negative stack**, pot inflated.

**Where (Hold’em):**  
`ProcessBettingActionCommandHandler.cs` — `ValidateAction` ~lines 440–442; `ExecuteAction` `case BettingActionType.Raise` ~lines 514–527.

### 1.2 Bug B — “raise” below the street bet lowers `CurrentBet`

**Example:** Round `CurrentBet = 20`, actor `CurrentBet = 0`, `ChipStack = 5`. Client sends `Raise` with `Amount = 5`.

- Min-raise check: `5 < 20 + LastRaiseAmount` **and** `5 < 5 + 0` → second clause false → validation passes.
- `ExecuteAction` sets `bettingRound.CurrentBet = 5`, **below** the previous 20.

Same pattern exists in every handler listed in §4.

### 1.3 Domain library gap (optional but recommended)

`src/CardGames.Poker/Betting/BettingRound.cs` has the same raise validation shape (no upper bound on `amount`) and `PokerPlayer.PlaceBet` only caps **increment** chips, not “raise to” total. API handlers do **not** call this class today, but tests in `CardGames.Poker.Tests/Betting/BettingRoundTests.cs` may need alignment if you fix the domain model for consistency.

---

## 2. Intended behavior (spec for the fix)

Define for a single actor on an active betting round:

| Symbol | Meaning |
|--------|---------|
| `streetBet` | `bettingRound.CurrentBet` |
| `playerBet` | `player.CurrentBet` |
| `stack` | `player.ChipStack` |
| `amount` | Command `Amount` (raise **to** total on this street) |
| `minRaiseTo` | `streetBet + bettingRound.LastRaiseAmount` (when `LastRaiseAmount == 0`, existing handlers still use this formula; do not change min-raise rules in this task unless tests require it) |
| `maxRaiseTo` | `playerBet + stack` (maximum total the player can put in this street) |

### 2.1 Raise validation (reject → `ProcessBettingActionError` / `InvalidAction`)

Apply **after** existing guards (`CurrentBet == 0` → use bet, etc.):

1. **`amount > maxRaiseTo`**  
   - Message (match `Bet` style):  
     `"Cannot raise more than your stack ({stack})"`  
     or explicitly:  
     `"Cannot raise to more than {maxRaiseTo} (your maximum total bet this street)"`  
   - Use the same wording pattern as `Bet when amount > player.ChipStack` in each handler.

2. **`amount <= streetBet`** (raise does not increase the street bet)  
   - Reject unless you intentionally normalize to `Call` / `AllIn` ( **do not** normalize in this task — reject).  
   - Suggested message:  
     `"Cannot raise to {amount} - must raise above the current bet of {streetBet}"`  
   - This blocks Bug B without allowing silent lowering of `CurrentBet`.

3. **Minimum raise (unchanged intent, explicit short all-in exception)**  
   - If `amount < minRaiseTo` **and** `amount < maxRaiseTo` → reject with existing min-raise message.  
   - If `amount == maxRaiseTo` **and** `amount < minRaiseTo` → **allow** (short all-in raise for less than a full min-raise). This matches common live-poker behavior and matches the existing partial pattern  
     `amount < minRaiseTo && amount < maxRaiseTo` in validators.

4. **Do not** allow `ChipStack` to go negative or `TotalContributedThisHand` / pot to increase by more than `stack` on a single action.

### 2.2 Raise execution (after validation)

In `ExecuteAction` for `BettingActionType.Raise`:

```csharp
var maxRaiseTo = player.CurrentBet + player.ChipStack;
var raiseTotal = Math.Min(amount, maxRaiseTo); // defensive; should equal amount if validation is correct
var raiseChips = raiseTotal - player.CurrentBet;

if (raiseChips > player.ChipStack)
    raiseChips = player.ChipStack; // defensive only

player.ChipStack -= raiseChips;
player.CurrentBet = playerBet + raiseChips; // == raiseTotal when consistent
player.TotalContributedThisHand += raiseChips;

var raiseIncrement = player.CurrentBet - streetBet; // use updated player.CurrentBet
// Update bettingRound.CurrentBet, LastRaiseAmount, LastAggressorIndex, RaiseCount as today
// Set player.IsAllIn = true when ChipStack == 0 after the action
```

**Invariants after any betting action:**

- `player.ChipStack >= 0`
- `player.CurrentBet <= maxRaiseTo` at time of action
- `bettingRound.CurrentBet >=` previous `streetBet` when action was `Raise` (strict increase)
- `chipsMoved` recorded on `BettingActionRecord` equals actual chips removed from stack
- Main pot `Amount` increases by `chipsMoved` only

### 2.3 Relationship to `AllIn`

Existing `AllIn` handling already caps by stack. After this fix:

- A client sending `Raise` with `amount == maxRaiseTo` should produce the same chip movement as an all-in that increases the bet (modulo aggressor / `LastRaiseAmount` edge cases already in `AllIn` branches).
- Do not remove or rewrite `AllIn` in this task; only ensure `Raise` cannot exceed stack.

---

## 3. Recommended implementation strategy

### 3.1 Prefer one shared helper (avoid 8 copy-paste drifts)

Add a small internal helper in the API layer, e.g.:

- **Path:** `src/CardGames.Poker.Api/Betting/BettingActionMath.cs` (or `Infrastructure/Betting/RaiseValidation.cs`)
- **Namespace:** `CardGames.Poker.Api.Betting` (or existing convention next to handlers)

Suggested API:

```csharp
internal static class BettingActionMath
{
    public static int GetMaxRaiseTo(int playerCurrentBet, int chipStack)
        => playerCurrentBet + chipStack;

    public static int GetMinRaiseTo(int streetCurrentBet, int lastRaiseAmount)
        => streetCurrentBet + lastRaiseAmount;

    /// <summary>Returns null if valid; otherwise error message for InvalidAction.</summary>
    public static string? ValidateRaise(
        int amount,
        int streetCurrentBet,
        int lastRaiseAmount,
        int playerCurrentBet,
        int chipStack);

    /// <summary>Computes chips moved and new player total bet for a validated raise.</summary>
    public static (int raiseTotal, int chipsMoved, int raiseIncrement) ComputeRaise(
        int amount,
        int streetCurrentBet,
        int playerCurrentBet,
        int chipStack);
}
```

Each `ProcessBettingActionCommandHandler` should:

1. Call `ValidateRaise` from its private `ValidateAction` switch for `BettingActionType.Raise`.
2. Call `ComputeRaise` from `ExecuteAction` `case BettingActionType.Raise` (or inline the same math once in the helper and use return values).

**Important:** Handlers use `CardGames.Poker.Api.Data.Entities.BettingActionType` and entity `BettingRound` / `GamePlayer`, not `CardGames.Poker.Betting.BettingRound`. The helper must use **int** fields from entities.

### 3.2 If not adding a helper

Apply **identical** validation and execution changes to **every** file in §4. Run a repo-wide search afterward:

```bash
rg "case BettingActionType.Raise:" src/CardGames.Poker.Api/Features/Games
rg "BettingActionType.Raise when amount <" src/CardGames.Poker.Api
```

All matches must include the new upper bound and `amount <= streetBet` guard.

---

## 4. Files that must be updated

### 4.1 API handlers (duplicate logic — all need the same fix)

| Game(s) using handler | Handler path |
|----------------------|--------------|
| Hold’em, Omaha, Nebraska, South Dakota, Red River, Klondike, Irish, Phil’s Mom, Crazy Pineapple, Bob Barker (human bets via router) | `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Hold the Baseball (timer/auto path only; human still uses HoldEm today) | `.../HoldTheBaseball/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Five Card Draw | `.../FiveCardDraw/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Twos/Jacks/Man with the Axe | `.../TwosJacksManWithTheAxe/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Seven Card Stud, **Razz**, **Pair Pressure**, **Tollbooth** (wrappers delegate here) | `.../SevenCardStud/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Good Bad Ugly | `.../GoodBadUgly/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Baseball | `.../Baseball/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Follow the Queen | `.../FollowTheQueen/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |

**Not in scope for separate edits:**  
`PairPressure/.../ProcessBettingActionCommandHandler.cs` and `Tollbooth/.../ProcessBettingActionCommandHandler.cs` — thin MediatR wrappers to Seven Card Stud handler.

### 4.2 Optional consistency

| File | Reason |
|------|--------|
| `src/CardGames.Poker/Betting/BettingRound.cs` | Same validation hole; keeps domain tests honest |
| `src/CardGames.Poker/Betting/PokerPlayer.cs` | Only if domain `Raise` path is fixed to validate `amount` before `PlaceBet(amount - CurrentBet)` |

### 4.3 Out of scope (document only)

- **Client/UI** clamping (`TablePlay.razor`, raise slider). Server must be correct even if UI sends bad amounts.
- **`AvailableActionsDto`** (`MinRaise` / `MaxBet` in `CardGames.Contracts/SignalR/PrivateStateDto.cs`): `MaxBet` is stack-only; raising UI may not expose `maxRaiseTo`. Optional follow-up: set `MaxBet` or add `MaxRaiseTo` for raise UI — not required to close the chip-conservation bug.
- Side pots, uncalled bet return, BB option, timer auto-action — separate audit items.

---

## 5. Tests to add or extend

### 5.1 Unit tests — Hold’em handler (primary)

File: `src/Tests/CardGames.Poker.Tests/Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandlerTests.cs`

Use existing `SeedGameAsync` helpers. Add:

| Test name (suggested) | Setup | Action | Assert |
|----------------------|-------|--------|--------|
| `Handle_RaiseAboveStack_ReturnsInvalidAction` | Betting phase, `streetBet=2`, actor `stack=100`, `playerBet=0` | `Raise, 1000` | `IsT1`, `InvalidAction`, message mentions stack; DB: `ChipStack` still 100, pot unchanged |
| `Handle_RaiseToMaxStack_Succeeds` | `streetBet=10`, actor `stack=50`, `playerBet=0` | `Raise, 50` | `IsT0`, `ChipStack==0`, `CurrentBet==50`, `IsAllIn`, pot += 50 |
| `Handle_RaiseBelowStreetBet_ReturnsInvalidAction` | `streetBet=20`, actor `stack=5`, `playerBet=0` | `Raise, 5` | `IsT1` (§2.1 rule 2); reload round: `CurrentBet` still 20 |
| `Handle_ShortAllInRaiseBelowMinRaise_Succeeds` | `streetBet=20`, `LastRaiseAmount=20`, actor `stack=25`, `playerBet=0` (`minRaiseTo=40`, `maxRaiseTo=25`) | `Raise, 25` | `IsT0`; `ChipStack==0`, `IsAllIn`, player `CurrentBet==25`, round `CurrentBet==25` (all-in for less than full min-raise) |

Note: `Raise, 5` with `streetBet=20` must **fail** — that is Bug B. Do not add a test that expects success for that case.

### 5.2 Unit tests — one non–Hold’em handler

Pick **Seven Card Stud** (`.../SevenCardStud/.../ProcessBettingActionCommandHandlerTests.cs`) or **Five Card Draw** and add **one** `RaiseAboveStack_ReturnsInvalidAction` test so wrapper games (Pair Pressure, Tollbooth) inherit coverage.

### 5.3 Shared helper tests (if created)

New file: `src/Tests/CardGames.Poker.Tests/Api/Betting/BettingActionMathTests.cs`  
Table-driven tests for `ValidateRaise` and `ComputeRaise` covering §2.1 rows.

### 5.4 Domain tests (optional)

`src/Tests/CardGames.Poker.Tests/Betting/BettingRoundTests.cs` — if `BettingRound.cs` is updated, add `ProcessAction_RaiseAboveStack_Fails`.

### 5.5 Run commands

```bash
cd src
dotnet test Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~ProcessBettingAction"
dotnet test Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~Betting"
```

---

## 6. Implementation checklist (order)

1. Add `BettingActionMath` (or equivalent) with unit tests.
2. Wire **HoldEm** `ValidateAction` + `ExecuteAction` to helper; run HoldEm handler tests.
3. Apply same wiring to the other seven handlers in §4.1 (copy-minimal: same two call sites per file).
4. Optionally align `CardGames.Poker/Betting/BettingRound.cs` + domain tests.
5. `rg` for any remaining `var raiseTotal = amount` without `Math.Min` / validation.
6. Manual sanity: integration test or single hand — raise max, raise over max (API returns 400/error body per endpoint mapping).

---

## 7. Endpoint error shape

Handlers return `OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>` with `Code = InvalidAction`. Ensure minimal API maps that to a **4xx** with message body (existing pattern — do not change unless tests fail).

---

## 8. Acceptance criteria

- [ ] No code path allows `GamePlayer.ChipStack < 0` after `Raise`.
- [ ] No `Raise` increases pot by more than the actor’s stack before the action.
- [ ] No `Raise` sets `bettingRound.CurrentBet` **below** its value before the action.
- [ ] `Raise` with `amount > player.CurrentBet + player.ChipStack` returns error; state unchanged.
- [ ] All eight handler files in §4.1 updated (or one helper used by all).
- [ ] New unit tests in §5.1 pass; full `ProcessBettingAction` test filter green.
- [ ] Pair Pressure / Tollbooth covered via Seven Card Stud handler tests (no duplicate handler edit).

---

## 9. Reference snippets (current buggy pattern)

**Validation (typical):**

```csharp
BettingActionType.Raise when amount < bettingRound.CurrentBet + bettingRound.LastRaiseAmount
                         && amount < player.ChipStack + player.CurrentBet =>
    $"Raise must be to at least {bettingRound.CurrentBet + bettingRound.LastRaiseAmount}",
// MISSING: amount > player.ChipStack + player.CurrentBet
// MISSING: amount <= bettingRound.CurrentBet
```

**Execution (typical):**

```csharp
case BettingActionType.Raise:
    var raiseTotal = amount;
    var raiseChips = raiseTotal - player.CurrentBet;
    player.ChipStack -= raiseChips;
    bettingRound.CurrentBet = raiseTotal;
    ...
```

Replace with §2.1–2.2 logic.

---

## 10. Audit cross-reference

- Parent audit: unconstrained raise affects all games listed in §4.1 (Hold’em family via shared HoldEm handler; stud family via Seven Card Stud handler; draw games via Five Card Draw / Twos Jacks handlers).
- This plan **does not** fix timer wrong-seat, BB option, or stud auto-action routing to Five Card Draw — separate work items.
