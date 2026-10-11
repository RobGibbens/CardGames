# Fix plan: mark short blind posts as all-in (`IsAllIn`)

**Audit finding:** “A short blind is not marked all-in.”  
**Audience:** LLM or engineer implementing the fix.  
**Scope:** Server-side blind posting for blind-based Hold’em-family games (`CollectBlindsAsync` / `PostBlindAsync`). Do not change blind amounts, seat assignment, or eligibility to start a hand unless tests require it.

---

## 1. Problem summary

When a player posts a **small blind (SB)** or **big blind (BB)** but their **chip stack is smaller than the blind amount**, the API correctly posts **only what they have** (`actualAmount = Min(amount, ChipStack)`), zeros their stack, and adds the chips to the pot. It does **not** set `GamePlayer.IsAllIn = true`.

That leaves a player with **`ChipStack == 0`**, **`HasFolded == false`**, and **`IsAllIn == false`** — an invalid combination for live poker and for this codebase’s betting logic.

### Symptom (user-visible)

- Seat shows **no** all-in styling (`TableSeat` uses `Seat.IsAllIn` for gold status ~397–398).
- Player may still appear in “can act” flows (timers, action order) despite having no chips.
- Downstream bugs (e.g. BB option / round closure) can worsen when a short blind is counted among `playersWhoCanAct` (see `docs/Auditing/fix-big-blind-option.md` §Related).

### Root cause (exact)

**File:** `src/CardGames.Poker.Api/GameFlow/BaseGameFlowHandler.cs`  
**Method:** `PostBlindAsync` (~513–531)

```csharp
var actualAmount = Math.Min(amount, player.ChipStack);
player.ChipStack -= actualAmount;
player.CurrentBet += actualAmount;
player.TotalContributedThisHand += actualAmount;
// pot updated — no IsAllIn when ChipStack becomes 0
```

**Contrast — correct pattern in ante collection** (same repo):

**File:** `src/CardGames.Poker.Api/Features/Games/Baseball/v1/Commands/CollectAntes/CollectAntesCommandHandler.cs` (~84–88)

```csharp
var wentAllIn = gamePlayer.ChipStack == 0;
if (wentAllIn)
{
    gamePlayer.IsAllIn = true;
}
```

The same `ChipStack == 0 → IsAllIn = true` pattern exists in **Five Card Draw**, **Seven Card Stud**, **Follow the Queen**, **Pair Pressure**, **Tollbooth**, **Twos Jacks**, etc. (`CollectAntes` handlers). **Blind posting is the outlier.**

### Domain library (reference, not used by API live path)

`CardGames.Poker.Betting.PokerPlayer` defines:

```csharp
public bool IsAllIn => ChipStack == 0 && !HasFolded;
```

(`PokerPlayer.cs` ~14)

`HoldEmGame.CollectBlinds()` uses `PlaceBet` on `PokerPlayer`, which implicitly satisfies “all-in” via **computed** `IsAllIn`. The API persists **`GamePlayer.IsAllIn`** as a separate flag reset in `StartHandCommandHandler.ResetPlayerStates` (~201) and set on betting actions when `ChipStack == 0` after call/raise/bet (~501–527 in Hold’em `ProcessBettingAction`).

---

## 2. Intended behavior (spec)

1. After posting a blind (SB or BB), if the player’s **`ChipStack == 0`** and they have **not** folded, set **`IsAllIn = true`**.
2. **Partial blind** is still valid: `CurrentBet == actualAmount` may be **less than** nominal SB/BB; `openingCurrentBet` for the betting round remains `Max(eligiblePlayers.CurrentBet)` from existing deal logic (`DealDrawStyleCardsAsync` ~334–336).
3. All-in blind players **cannot** take further betting actions (same as post-action all-in).
4. **Action rotation:** `FindNextActivePlayer` / `playersWhoCanAct` already skip `IsAllIn` in Hold’em `ProcessBettingAction` (~616, ~574). Marking the flag makes rotation correct **before** the first voluntary action.
5. **UI:** `TableStateBuilder` maps `IsAllIn = gamePlayer.IsAllIn` (~665) → clients show all-in state.
6. **Starting a hand:** Players with `ChipStack > 0` remain eligible (`StartHandCommandHandler` ~63–67). A player with 3 chips can post a 3-chip short SB and become all-in.

### Scenarios

| Case | Stack | SB | BB | After SB post | After BB post |
|------|-------|----|----|---------------|---------------|
| Short SB | 3 | 5 | 10 | `CurrentBet=3`, `ChipStack=0`, **`IsAllIn=true`** | BB normal |
| Short BB | 7 | 5 | 10 | SB normal | `CurrentBet=7`, `ChipStack=0`, **`IsAllIn=true`** |
| Both short (rare) | SB 2, BB 4 | 5 | 10 | SB all-in 2 | BB all-in 4 |
| Full blinds | 1000 | 5 | 10 | `IsAllIn=false` | `IsAllIn=false` |

### Heads-up

`CollectBlindsAsync` posts dealer as SB and other seat as BB (~493–497). Short-stack rules apply to either seat.

---

## 3. Recommended fix

### 3.1 Primary change (single location)

In `PostBlindAsync`, after deducting chips:

```csharp
player.ChipStack -= actualAmount;
player.CurrentBet += actualAmount;
player.TotalContributedThisHand += actualAmount;

if (player.ChipStack == 0 && !player.HasFolded)
{
    player.IsAllIn = true;
}
```

**Do not** set `IsAllIn` when `actualAmount == 0` (no chips to post); early `return` when `amount <= 0` already handles zero blind config.

### 3.2 Optional consistency (same PR if trivial)

| Item | Action |
|------|--------|
| Extract helper | `BlindPosting.SetAllInIfPostedLastChips(GamePlayer player)` in `GameFlow/BlindPosting.cs` to mirror ante handlers’ inline check |
| Domain `HoldEmGame.CollectBlinds` | No change required for API; optional comment that API uses `PostBlindAsync` |
| `BettingActionRecord` for blinds | **Out of scope** — no records today; forced blind audit trail is a separate feature |
| `PotContribution` on blind post | **Out of scope** — side-pot eligibility may still use contributions from betting actions only; document if side-pot bugs persist |

### 3.3 Games affected

All flow handlers that call `CollectBlindsAsync` → `PostBlindAsync`:

- Hold’em, Omaha, Nebraska, South Dakota, Red River, Klondike, Irish Hold’em, Phil’s Mom, Crazy Pineapple, Bob Barker, Hold the Baseball (`GameFlow/*FlowHandler.cs` grep `CollectBlindsAsync`).

One fix in `BaseGameFlowHandler` covers all variants.

---

## 4. Downstream behavior to verify (not necessarily change)

| Consumer | Expected after fix |
|----------|-------------------|
| `ProcessBettingAction` `playersWhoCanAct` | Short blind excluded |
| `FindNextActivePlayer` | Skips short blind |
| `AreAllPlayersAllIn` | May correctly detect all-in runout when appropriate |
| Auto-action / timer | Should not assign actions to `IsAllIn` seats (verify `AutoActionService` respects flag) |
| `ValidateAction` | Actor with `ChipStack == 0` should not be current actor if `IsAllIn` |

If timer still targets a `ChipStack == 0` seat with `IsAllIn == false`, that is **this** bug; if `IsAllIn` is true but timer still fires, file separate timer audit.

---

## 5. Implementation checklist

- [ ] Update `PostBlindAsync` with `IsAllIn` when `ChipStack == 0`.
- [ ] Add unit/integration tests (§6).
- [ ] Run existing `StartHand` integration tests (no regression on full stacks).
- [ ] Manual: short SB seat shows gold all-in status after hand starts.

---

## 6. Tests

### 6.1 Integration — short small blind

**File:** extend `src/Tests/CardGames.IntegrationTests/Handlers/StartHandCommandHandlerIntegrationTests.cs` or add `HoldEmShortBlindTests.cs`

**Arrange:**

- 4-player HOLDEM, `SmallBlind=5`, `BigBlind=10`, normal stacks except **SB seat** `ChipStack=3` before `StartHand`.
- Set `DealerPosition` so seat 1 is SB (match existing test layout: dealer 0 → SB 1, BB 2).

**Act:** `StartHandCommand`

**Assert:**

- SB seat: `ChipStack == 0`, `CurrentBet == 3`, **`IsAllIn == true`**.
- BB seat: `ChipStack == 990`, `CurrentBet == 10`, `IsAllIn == false`.
- Main pot includes `3 + 10 = 13`.
- Phase `PreFlop`; first actor still UTG (not short SB).

### 6.2 Integration — short big blind

**Arrange:** BB seat `ChipStack=7`, blinds 5/10.

**Assert:** BB `CurrentBet=7`, `ChipStack=0`, `IsAllIn=true`; pot includes SB + 7.

### 6.3 Unit — `PostBlindAsync` (optional)

If extracting testable helper or subclassing is heavy, integration tests above are sufficient. Optional: test `BaseGameFlowHandler` via small fixture with in-memory `CardsDbContext`, call `CollectBlindsAsync` on a game with short SB.

### 6.4 Regression — full stacks

Existing test `Handle_HoldTheBaseball_AutoDealsHoleCardsAndCollectsBlinds_TransitionsToPreFlop` (~383–428): assert `IsAllIn` remains false for blind seats with sufficient chips.

### Commands

```bash
cd src
dotnet test Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~StartHand|FullyQualifiedName~ShortBlind"
dotnet test Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~CollectAntes"
```

---

## 7. Acceptance criteria

- [ ] Posting SB or BB with fewer chips than the blind amount sets **`IsAllIn = true`** when stack reaches 0.
- [ ] Full-stack blind posts leave **`IsAllIn = false`**.
- [ ] Public table state / seat UI can show all-in for short blind (via existing `IsAllIn` DTO).
- [ ] Short blind is not treated as a player who can still act in betting-round logic that filters on `IsAllIn`.
- [ ] Behavior matches ante “went all-in” semantics used elsewhere in the API.

---

## 8. File reference

| Area | Path |
|------|------|
| **Bug** | `CardGames.Poker.Api/GameFlow/BaseGameFlowHandler.cs` (`PostBlindAsync`, `CollectBlindsAsync`) |
| **Pattern** | `Features/Games/Baseball/v1/Commands/CollectAntes/CollectAntesCommandHandler.cs` |
| Reset flag each hand | `Features/Games/HoldEm/v1/Commands/StartHand/StartHandCommandHandler.cs` (`ResetPlayerStates`) |
| Set flag on bet actions | `Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` (`ExecuteAction`) |
| UI | `CardGames.Poker.Api/Services/TableStateBuilder.cs`; `CardGames.Poker.Web/Components/Shared/TableSeat.razor` |
| Entity | `CardGames.Poker.Api/Data/Entities/GamePlayer.cs` (`IsAllIn`) |
| Domain reference | `CardGames.Poker/Betting/PokerPlayer.cs` (computed `IsAllIn`) |
| Related plan | `docs/Auditing/fix-big-blind-option.md` (round closure vs `playersWhoCanAct`) |

---

## 9. Out of scope

- Side-pot / `PotContribution` rows for blind posts.
- Forced `BettingActionRecord` entries for blinds (`IsForced` exists on entity).
- Increasing min buy-in to always cover blinds.
- Tournament ante vs blind mixing.

---

## 10. Manual QA

1. Create Hold’em table, SB=5, BB=10.
2. Set one player’s stack to **3** chips (rebuy/edit chips if needed).
3. Start hand; confirm that player is SB (adjust dealer if needed).
4. Confirm seat shows **all-in** styling and stack **0**, bet **3**.
5. Confirm action passes to next eligible player (not short SB).
6. Repeat with BB stack **7** and verify BB all-in for **7**.
