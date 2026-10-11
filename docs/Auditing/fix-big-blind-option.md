# Fix plan: preserve the big blind’s pre-flop option (round closes too early)

**Audit finding:** “Wrong action closing: the big blind loses its option.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** Server-side `IsRoundComplete` / betting-round closure in blind-based Hold’em-family handlers. Do not change blind posting amounts or min-raise rules unless required by tests.

---

## 1. Problem summary

In Texas Hold’em (and variants that reuse the same handler), the **big blind (BB)** is a **live blind**. When the action is **not raised** pre-flop, every caller must match the big blind, then the **BB still gets a turn** to **check** or **raise** (“BB option”).

Product rules: `docs/TexasHoldEmRules.md` — action starts left of the BB; the BB is a live bet; betting continues until all active players have **placed equal bets** and **completed their action** for the round.

Today the API often **ends the pre-flop betting round when the last caller *before* the BB acts**, skipping the BB entirely. The table jumps to the flop while `CurrentActorIndex` would have been the BB seat on the next action.

### Symptom (user-visible)

- 3+ players, pre-flop: UTG… callers limp/call, SB completes, **flop is dealt immediately**.
- BB never receives action; cannot check-raise a limped pot.
- May coincide with `RoundComplete = true` on the SB’s call in API responses.

### Root cause (logic bug, not blind posting)

**File:** `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`  
**Method:** `IsRoundComplete` (~609–637)

After each action the handler:

1. Increments `bettingRound.PlayersActed`.
2. Computes `nextPlayerIndex = FindNextActivePlayer(...)`.
3. Sets `roundComplete = IsRoundComplete(activePlayers, bettingRound, nextPlayerIndex)`.

The faulty branch:

```csharp
if (bettingRound.PlayersActed >= playersWhoCanAct.Count && allMatched)
{
    return true;
}
```

`PlayersActed` is a **single counter** of how many actions were processed this round.  
`playersWhoCanAct` is **every non-folded, non-all-in** player who must eventually act.

When the **small blind (or last caller before BB)** calls and `nextPlayerIndex` is the **BB seat**, typically:

- `PlayersActed == playersWhoCanAct.Count` (e.g. 3 of 3 in a 3-way pot after SB calls).
- `allMatched == true` (everyone’s `CurrentBet` equals `bettingRound.CurrentBet`, including BB who posted the blind).

→ `IsRoundComplete` returns **true** even though the **BB has not taken a voluntary action** this round.

The **last aggressor** branch does not help on a **limped** pot (`LastAggressorIndex == -1`).

**Important:** Posting blinds does **not** increment `PlayersActed` in the API (only `ProcessBettingAction` does). The bug is purely the **`>= Count` shortcut**, not double-counting the blind post.

### Duplicate handler (must fix too)

**File:** `src/CardGames.Poker.Api/Features/Games/HoldTheBaseball/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`  
**Method:** `IsRoundComplete` (~406–427) — **same** `PlayersActed >= playersWhoCanAct.Count` pattern.

Hold the Baseball **timer/auto** path uses this handler; human bets still use Hold’em handler today (`fix-raise-stack-cap.md`).

### What is already correct (do not break)

- First pre-flop actor: `BaseGameFlowHandler.DealDrawStyleCardsAsync` uses `FindFirstActivePlayerAfterBigBlind` (~350–352).
- Blinds: `CollectBlindsAsync` / `PostBlindAsync` set `CurrentBet` on SB/BB; opening `BettingRound.CurrentBet` seeded from max posted blind (~334–336).
- Post-flop rounds: new `BettingRound` with `CurrentBet = 0`, `PlayersActed = 0` (~343–360).
- `TableStateBuilder` blind indicators (separate from round completion).

### Related but out of scope (optional follow-ups)

| Item | Location | Note |
|------|----------|------|
| Domain `BettingRound` marks forced BB as “acted” at construction | `src/CardGames.Poker/Betting/BettingRound.cs` ~55–60 | API does not use this class for live play; fix if domain tests/game sim should match |
| `PlayersActed` not reset on raise/bet | Hold’em `ExecuteAction` | Re-open action may need per-player acted reset (like domain `_playersWhoActed.Clear()` on raise); separate from BB option but share same helper |
| Other handlers (Stud, Draw, …) | Same `PlayersActed >= Count` pattern | No BB option; still worth aligning completion logic when extracting shared helper |

---

## 2. Intended behavior (spec)

### 2.1 Pre-flop (blind-based streets)

Let `playersWhoCanAct` = active, not folded, not all-in.

A betting round is **complete** when **all** of the following hold:

1. At most one non-folded player remains, **or** no one can act (all-in/folded), **or**
2. Every player in `playersWhoCanAct` has **`CurrentBet == bettingRound.CurrentBet`**, **and**
3. **Every** player in `playersWhoCanAct` has recorded **at least one voluntary betting action** in **this** `BettingRound` (check/call/raise/fold/all-in via `ProcessBettingAction`), **and**
4. **Closing the action on the last aggressor:** if `LastAggressorIndex >= 0` and `nextPlayerIndex == LastAggressorIndex` with all bets matched, the round may complete **without** another action from the aggressor (they already bet/raised).

**BB option corollary:** The BB **must** satisfy (3) on an unraised pre-flop round. Posting the big blind is **not** a substitute for a voluntary action in the API model (no blind `BettingActionRecord` today).

### 2.2 Post-flop

Same rules; first actor is left of the button (`FindFirstActivePlayerAfterDealer`). The per-player “has acted” rule still applies; the BB option case is pre-flop-specific in practice but the **general** rule fixes post-flop too (no early close when one player has not acted).

### 2.3 Heads-up pre-flop

Dealer/SB acts first, BB second. After SB calls, `nextPlayerIndex` is BB; round must **not** complete until BB checks/raises/folds.

---

## 3. Concrete scenarios (use in tests)

### 3.1 Three-handed limped pot (primary regression)

**Seats (example):** Dealer = 0, SB = 1, BB = 2. Blinds 5/10. First to act = seat 0 (UTG in 3-max = button).

| Step | Actor | Action | After | Round complete? |
|------|-------|--------|-------|-----------------|
| 1 | 0 | Call 10 | `PlayersActed=1`, next=1 | No |
| 2 | 1 (SB) | Call 10 | `PlayersActed=2`, next=**2 (BB)** | **No** (today: **Yes** — bug) |
| 3 | 2 (BB) | Check | `PlayersActed=3` | Yes → deal flop |

### 3.2 Four-handed with one fold

Dealer 0, SB 1, BB 2, UTG 3. UTG folds, BTN calls, SB calls, **next BB** — must not complete until BB acts.

### 3.3 Raised pot — BB is last aggressor

UTG raises, all call to BB, BB already raised → action returns to UTG with `LastAggressorIndex == UTG`; existing branch may close when `nextPlayerIndex == LastAggressorIndex` and `allMatched`. **Do not** require BB to act again after everyone called their raise.

### 3.4 BB faces a raise

UTG raises, … action to BB with amount to call — BB must act (call/raise/fold). Completion only after BB’s action (or last-aggressor closure).

---

## 4. Recommended fix

### 4.1 Replace counter-based completion with per-player “has acted”

**Do not** rely on `PlayersActed >= playersWhoCanAct.Count`.

**Option A (recommended):** Use `BettingRound.Actions` (`BettingActionRecord` per voluntary action).

After adding the current record in `Handle`:

```csharp
var actedPlayerIds = bettingRound.Actions
    .Select(a => a.GamePlayerId)
    .Append(currentPlayer.Id) // if navigation not updated yet
    .ToHashSet();

bool allWhoCanActHaveActed = playersWhoCanAct
    .All(p => actedPlayerIds.Contains(p.Id));
```

Then:

```csharp
private bool IsRoundComplete(
    List<GamePlayer> activePlayers,
    BettingRound bettingRound,
    int nextPlayerIndex,
    IReadOnlySet<Guid> actedPlayerIds)
{
    var playersInHand = activePlayers.Count(p => !p.HasFolded);
    if (playersInHand <= 1) return true;

    var playersWhoCanAct = activePlayers
        .Where(p => !p.HasFolded && !p.IsAllIn)
        .ToList();
    if (playersWhoCanAct.Count == 0) return true;

    var allMatched = playersWhoCanAct.All(p => p.CurrentBet == bettingRound.CurrentBet);
    if (!allMatched) return false;

    if (bettingRound.LastAggressorIndex >= 0
        && nextPlayerIndex == bettingRound.LastAggressorIndex)
    {
        return true;
    }

    return playersWhoCanAct.All(p => actedPlayerIds.Contains(p.Id));
}
```

**Remove** the `PlayersActed >= playersWhoCanAct.Count` branch entirely (or make `PlayersActed` a denormalized count only for telemetry, not completion).

**Option B (minimal):** Pre-flop-only guard:

```csharp
if (IsPreFlop(bettingRound)
    && nextPlayerIndex == GetBigBlindSeatIndex(game, activePlayers)
    && !actedPlayerIds.Contains(bbPlayer.Id))
{
    return false;
}
```

Option A is more general and fixes the same class of bug if a non-BB player were skipped.

### 4.2 Shared helper (align with raise-stack plan)

Add to the same shared module proposed in `docs/Auditing/fix-raise-stack-cap.md`:

- **Path:** `src/CardGames.Poker.Api/Betting/BettingRoundCompletion.cs` (or methods on `BettingActionMath`)

```csharp
internal static bool IsRoundComplete(
    Game game,
    List<GamePlayer> activePlayers,
    BettingRound bettingRound,
    int nextPlayerIndex,
    IReadOnlySet<Guid> actedPlayerIds);
```

Wire **Hold’em** and **Hold the Baseball** handlers first. Optionally migrate Stud/Draw handlers later (same bug shape, different street semantics).

### 4.3 Reset “acted” set on bet/raise (recommended in same PR)

Domain `BettingRound` clears `_playersWhoActed` on bet/raise and only adds the aggressor. API should mirror:

- On `Bet` / `Raise` / all-in that increases `CurrentBet` above previous street bet: treat acted set as **only players who acted since the last full bet level change**, **or** reload acted IDs from actions after last raise (simpler: on raise, delete from DB is heavy — in-memory for current round: clear `PlayersActed` to 1 and rely on action records with `ActionOrder` after last raise).

**Pragmatic approach:** When evaluating `actedPlayerIds`, include all `BettingActionRecord` for the round **except** when determining closure after a raise, only players with an action **after** the last raise action need to have acted — **or** keep using `LastAggressorIndex` closure branch for raised pots and per-player acted for limped pots.

Minimum for BB option: **per-player acted** + keep **last aggressor** branch.

### 4.4 `PlayersActed` column

Can remain incremented for backwards compatibility / UI, but **must not** drive completion. Update `GetCurrentBettingRound` if UI displays it — optional.

---

## 5. Games affected (routing)

All variants that use **Hold’em** `ProcessBettingActionCommandHandler` (see `fix-raise-stack-cap.md` §4.1):

- HOLDEM, OMAHA, NEBRASKA, SOUTHDAKOTA, REDRIVER, KLONDIKE, IRISHHOLDEM, PHILSMOM, CRAZYPINEAPPLE, BOBBARKER (human bets), etc.

**Also:** `HoldTheBaseball` **dedicated** handler (auto/timer).

**Not affected:** Seven Card Stud / Razz / Draw (no BB option).

---

## 6. Implementation order (checklist)

1. Add `BettingRoundCompletion.IsRoundComplete(...)` with unit tests (pure function).
2. Update **Hold’em** `ProcessBettingActionCommandHandler`:
   - Build `actedPlayerIds` after each action.
   - Replace private `IsRoundComplete` body with shared helper.
3. Update **Hold the Baseball** handler identically.
4. Add integration test: 3-handed limped pot, assert flop **not** dealt after SB call; assert BB can check.
5. Add integration test: raised pot still closes correctly at last aggressor.
6. (Optional) Reset acted tracking on raise in `ExecuteAction` + tests for re-open.
7. (Optional) Domain `BettingRound.CheckIfRoundComplete` — require voluntary act for BB when using forced-bet constructor.

---

## 7. Tests

### 7.1 Unit — `BettingRoundCompletionTests`

**File:** `src/Tests/CardGames.Poker.Tests/Api/Betting/BettingRoundCompletionTests.cs`

| Case | actedPlayerIds | next | LastAggressor | Expected |
|------|----------------|------|---------------|----------|
| Limped, SB just called, BB not in set | {UTG, SB} | BB seat | -1 | false |
| Limped, all three acted | {0,1,2} | -1 or BB | -1 | true |
| Raise, action back on raiser, all matched | all callers | raiser seat | raiser | true |
| One player folded | … | … | … | adjust `playersWhoCanAct` |

Use synthetic `GamePlayer` / `BettingRound` entities (no DB).

### 7.2 Unit — Hold’em handler

**File:** extend `ProcessBettingActionCommandHandlerTests.cs`

Seed pre-flop round with 3 players, blinds posted, `CurrentBet=10`, actor SB, BB seat known.  
SB `Call` → assert `RoundComplete == false`, `NextPlayerIndex == bbSeat`.

### 7.3 Integration

**File:** new or `src/Tests/CardGames.IntegrationTests/Games/HoldEm/HoldEmBettingRoundTests.cs`

1. `StartHand` on 3-player HOLDEM table.
2. Mediator: player after BB calls, SB calls.
3. Assert `game.CurrentPhase` still `PreFlop` (or `RoundComplete` false on last response).
4. BB `Check` → `RoundComplete` true, phase `Flop`.

Use `DatabaseSeeder` / existing Hold’em lifecycle patterns.

### 7.4 Regression — post-flop

Single check-around on flop: four players all check — round completes only after **fourth** check, not third.

### Commands

```bash
cd src
dotnet test Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~BettingRoundCompletion|FullyQualifiedName~ProcessBettingAction"
dotnet test Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~HoldEmBetting|FullyQualifiedName~BigBlind"
```

---

## 8. Acceptance criteria

- [ ] Pre-flop limped/called pot: **BB always** receives an action before flop.
- [ ] BB can **check** or **raise** after full limp.
- [ ] No regression: raised pots close when action returns to last aggressor with all bets matched.
- [ ] Hold’em-family games sharing the handler behave consistently.
- [ ] Hold the Baseball auto-bet path uses the same completion rules.
- [ ] Unit tests document the SB-call / BB-waits scenario explicitly.

---

## 9. File reference

| Area | Path |
|------|------|
| **Bug** | `Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` (`IsRoundComplete`, `PlayersActed++`) |
| **Duplicate bug** | `Features/Games/HoldTheBaseball/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Blind / first actor | `GameFlow/BaseGameFlowHandler.cs` (`CollectBlindsAsync`, `FindFirstActivePlayerAfterBigBlind`, opening `BettingRound`) |
| Action records | `Data/Entities/BettingActionRecord.cs`, `BettingRound.Actions` |
| Rules text | `docs/TexasHoldEmRules.md` (pre-flop, live BB) |
| Domain reference | `CardGames.Poker/Betting/BettingRound.cs` (`CheckIfRoundComplete`, per-index `_playersWhoActed`) |
| Related plan | `docs/Auditing/fix-raise-stack-cap.md` (shared `BettingActionMath` / handler list) |

---

## 10. Out of scope

- Uncalled bet return to caller.
- Side-pot eligibility on all-in for less than BB.
- Timer acting for wrong seat.
- Client raise slider / `AvailableActionsDto` (server must be correct regardless).

---

## 11. Manual QA

1. Create **3-player** Hold’em cash table, blinds 5/10.
2. Pre-flop: button calls, SB calls — confirm **BB** still has action (check/raise buttons), flop **not** dealt.
3. BB checks — flop dealt.
4. Repeat with **4 players**, one fold pre-flop, ensure BB option after calls.
5. Pre-flop: UTG raises, others call — confirm round ends correctly when action completes (no infinite loop).
