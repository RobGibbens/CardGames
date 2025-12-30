## Plan: Twos/Jacks/Axe wilds + split pot

Implement Twos, Jacks, Man with the Axe as a true wild-card + dual-payout (7s) draw variant. Reuse the existing wild-card evaluation machinery (`WildCardHandEvaluator` + the Baseball pattern), extend the showdown flow to compute two parallel “winner sets” (7s-half + best-hand-half) per pot (including side pots), persist the dual payouts in hand history, expose them via SignalR table state, and update the Blazor UI to enable the variant and render wild indicators + a dual-payout showdown overlay. Also fix the existing Twos/Jacks/Axe CreateGame handler bug that incorrectly uses the Five Card Draw code.

### Steps {3–6 steps, 5–20 words each}
1. Add Twos/Jacks/Axe wild-card rules + draw-hand evaluator in [CardGames.Poker](CardGames.Poker/Hands/DrawHands/) mirroring `BaseballWildCardRules` and `BaseballHand`.
2. Update `TwosJacksManWithTheAxeGame.PerformShowdown()` in [TwosJacksManWithTheAxeGame.cs](CardGames.Poker/Games/TwosJacksManWithTheAxe/TwosJacksManWithTheAxeGame.cs) to split each pot into two awards.
3. Extend pot splitting logic around `PotManager.AwardPots()` in [PotManager.cs](CardGames.Poker/Betting/PotManager.cs) via a “dual winners” callback (side-pots included).
4. Persist dual payouts in API hand history + table state in [HandHistoryRecorder.cs](CardGames.Poker.Api/Services/HandHistoryRecorder.cs) and `TableStateBuilder.BuildShowdownPublicDto()` in [TableStateBuilder.cs](CardGames.Poker.Api/Services/TableStateBuilder.cs).
5. Fix Twos/Jacks/Axe create-game wiring and availability gating in [CreateGameCommandHandler.cs](CardGames.Poker.Api/Features/Games/TwosJacksManWithTheAxe/v1/Commands/CreateGame/CreateGameCommandHandler.cs) and [CreateTable.razor](CardGames.Poker.Web/Components/Pages/CreateTable.razor).
6. Update Blazor showdown UX in [ShowdownOverlay.razor](CardGames.Poker.Web/Components/Shared/ShowdownOverlay.razor) and card rendering to show wild markers + dual awards.

### Further Considerations {1–3, 5–25 words each}
1. Tie rules: split each half-pot independently; odd chip assignment deterministic by seat order (recommend dealer-left first).
2. “Natural pair of sevens” definition: exactly two non-wild 7s; wild-substituted 7s don’t qualify.
3. If only one player remains by fold: award full pot to that player; skip 7s half-pot check.

---

## Detail to execute each step

### 1) Wild cards: domain + evaluation (CardGames.Poker)
- **Add a wild-card rule class** analogous to `BaseballWildCardRules`:
  - New file near [BaseballWildCardRules.cs](CardGames.Poker/Hands/WildCards/BaseballWildCardRules.cs), e.g. `TwosJacksManWithTheAxeWildCardRules`.
  - `DetermineWildCards(hand)` should return cards where:
    - `Symbol == Symbol.Deuce` (all 2s),
    - `Symbol == Symbol.Jack` (all Jacks),
    - and `Symbol == Symbol.King && Suit == Suit.Diamonds` (King♦).
- **Create a draw-hand implementation that applies wild substitutions**, following the `BaseballHand` pattern but for 5-card draw:
  - Consider a new `FiveCardHand` subtype such as `TwosJacksManWithTheAxeDrawHand` (in [DrawHands](CardGames.Poker/Hands/DrawHands/)).
  - It should expose:
    - `WildCards` (actual wilds in the dealt 5 cards),
    - `EvaluatedBestCards` (best substituted 5 cards),
    - `Type`/`Strength` computed using `WildCardHandEvaluator.EvaluateBestHand(Cards, WildCards, Ranking)`.
- **Decide where to plug it in**:
  - Replace `new DrawHand(gp.Hand)` usage in `TwosJacksManWithTheAxeGame.PerformShowdown()` with the new wild-aware hand type.
  - Update any API-side evaluation paths that assume `DrawHand` == draw game evaluation (see `TableStateBuilder` below).

Edge cases to handle in evaluation:
- 5 wilds: `WildCardHandEvaluator` already returns Five of a Kind; ensure UI/description formatting supports it.
- Wild cards duplicating ranks/suits: current wild evaluator freely constructs cards; ensure no downstream assumes a real deck uniqueness at showdown display (usually fine for wild poker).

---

### 2) Natural pair of sevens pays half (CardGames.Poker showdown logic)
- In [TwosJacksManWithTheAxeGame.cs](CardGames.Poker/Games/TwosJacksManWithTheAxe/TwosJacksManWithTheAxeGame.cs), enhance `PerformShowdown()`:
  - Compute, for each eligible player at showdown:
    - `HasNaturalPairOfSevens`: exactly two 7s present in the *original* dealt cards, and those sevens are **not** wild cards (so 7s themselves are never wild in this variant, but this keeps the rule explicit).
    - A “best-hand” strength using the new wild-aware hand.
  - Split the pot **per pot/side-pot** into two “award pools”:
    - **Sevens pool** = half of that pot amount (see odd-chip rules below).
    - **Hand pool** = remainder of that pot amount.
  - Award the sevens pool to all eligible players who have a natural pair of sevens (can be 0, 1, or multiple).
    - If **no** eligible players have natural 7s, define behavior:
      - Recommended: roll the sevens pool into the hand pool (“no qualifier => best hand takes all”).
      - Alternative: carry over (not typical); I recommend roll-in for simplicity and player expectation.
  - Award the hand pool to best evaluated hand winner(s) among eligible players (ties split).
- Ensure the win-by-fold branch remains “winner gets entire pot” regardless of sevens.

Recommended “natural pair of sevens” tie rule:
- If multiple players qualify, they split the sevens pool evenly.
- If an odd chip exists within the sevens pool, award it by deterministic ordering (see PotManager step).

---

### 3) Pot splitting including side pots (PotManager integration)
Current `PotManager.AwardPots(Func<IEnumerable<string>, IEnumerable<string>> determineWinners)` (see [PotManager.cs](CardGames.Poker/Betting/PotManager.cs)) awards each pot entirely to a single winner-set.

To support **two awards per pot**, implement one of these approaches:

- **Option A (preferred): Add a new method** on `PotManager`:
  - `AwardPotsSplit(Func<IEnumerable<string>, (IEnumerable<string> sevensWinners, IEnumerable<string> handWinners, bool sevensPoolRollsToHand)> determineWinners)`
  - For each internal `Pot`:
    - Split `pot.Amount` into `sevensPool` and `handPool`.
    - If `sevensWinners` empty and rule is roll-in, add sevensPool into handPool.
    - Split each pool across its winner set using the same remainder distribution scheme currently used.
  - Return a payout dictionary (total per player), plus (important!) return “breakdown” data for API/UI:
    - per player: `{ sevensAmountWon, handAmountWon, totalAmountWon }`
    - per pot: `{ potIndex, sevensPool, handPool, sevensWinners, handWinners }` (useful for debugging/UI overlay).
- **Option B: Keep PotManager unchanged**, and in `PerformShowdown()` iterate `PotManager.Pots` manually, duplicating payout math. This is faster to ship but repeats logic and can drift from existing tests.

Also align odd-chip distribution:
- Today remainder chips are handed out by iterating the `winners` list order. Define a deterministic order:
  - Recommended: sort winners by seat position (dealer-left first) or by seat index.
  - Since `PotManager` currently only knows names, you can:
    - either pass winners already ordered from the game (requires mapping name→seat),
    - or add an overload that accepts a name ordering function.

Add/extend tests (existing tests exist at `Tests/CardGames.Poker.Tests/Betting/PotManagerTests.cs`) to cover:
- side pot + sevens pool split
- multiple sevens winners
- rollover when no sevens qualifier
- odd-chip deterministic remainder rules across both pools

---

### 4) DB entities/migrations + API DTOs + SignalR table state (CardGames.Poker.Api + Contracts)
#### 4a) Fix Twos/Jacks/Axe game type code bug (blocks correct wiring)
- In [CreateGameCommandHandler.cs](CardGames.Poker.Api/Features/Games/TwosJacksManWithTheAxe/v1/Commands/CreateGame/CreateGameCommandHandler.cs):
  - Stop using `private const string FiveCardDrawCode = "FIVECARDDRAW";`
  - Use `PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode` (from [PokerGameMetadataRegistry.cs](CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs)).
  - Set `Game.CurrentPhase` to `nameof(TwosJacksManWithTheAxePhase.WaitingToStart)` (instead of `FiveCardDrawPhase`).
  - When creating the `GameType`, set:
    - `Name = "Twos, Jacks, Man with the Axe"`
    - `Code = TWOSJACKSMANWITHTHEAXE`
    - `WildCardRule` to a fitting enum value (likely `FixedRanks` even though one wild is suit-specific; consider adding a new enum value or encode via `VariantSettings`).
    - `VariantSettings` JSON for: wild definition + “sevens half-pot enabled”.
  - This addresses the “CreateGame handler bug (using FiveCardDrawCode)” requirement.

#### 4b) Persist dual payouts in hand history
Hand history currently stores:
- total pot
- winners list with `AmountWon` (single total)
- per-player `NetChipDelta`

To support dual payouts cleanly:
- Minimal approach: keep winners as totals, and embed breakdown in:
  - `HandHistory.WinningHandDescription` (not great), or
  - a new JSON column on `HandHistory` (recommended): `SettlementDetailsJson` containing:
    - per player: `SevensAmountWon`, `HighHandAmountWon`, `Total`
    - metadata: `SevensRuleApplied`, `SevensWinners`, `HighHandWinners`, `RolledOver`
- Add the new column via EF migration in [CardGames.Poker.Api/Migrations](CardGames.Poker.Api/Migrations/) and update [HandHistory.cs](CardGames.Poker.Api/Data/Entities/HandHistory.cs) plus configuration.
- Update [HandHistoryRecorder.cs](CardGames.Poker.Api/Services/HandHistoryRecorder.cs) to accept new parameters and persist them.

#### 4c) Expose dual payouts via SignalR (`TableStateBuilder`)
- `TableStateBuilder.BuildShowdownPublicDto()` (see [TableStateBuilder.cs](CardGames.Poker.Api/Services/TableStateBuilder.cs)) currently:
  - evaluates with `new DrawHand(coreCards)`
  - sets `AmountWon = 0` and determines winners by max strength only
- Update it to be variant-aware by `game.GameType.Code`:
  - If `TWOSJACKSMANWITHTHEAXE`, evaluate with the new wild-aware draw hand, and determine:
    - sevens winners (natural pair check)
    - high-hand winners
  - Populate a richer showdown DTO:
    - per player: `AmountWon`, plus `SevensAmountWon` and `HighHandAmountWon`
    - per showdown: optionally include `SevensPoolAmount` / `HighHandPoolAmount` (at least totals, ideally per side pot breakdown)
- This requires expanding contracts in [TableStatePublicDto.cs](CardGames.Contracts/SignalR/TableStatePublicDto.cs):
  - Add optional fields to `ShowdownPublicDto` and/or `ShowdownPlayerResultDto`:
    - `public int SevensAmountWon { get; init; }`
    - `public int HighHandAmountWon { get; init; }`
    - `public bool IsSevensWinner { get; init; }`
    - `public bool IsHighHandWinner { get; init; }`
    - `public IReadOnlyList<int>? WildCardIndexes { get; init; }` or card-level `IsWild` marker (see UI section).
- Make sure any refit clients that consume contracts update cleanly (generated code lives under `CardGames.Contracts/RefitInterface.v1.cs` and `CardGames.Poker.Refitter/Output/...`).

---

### 5) Blazor UI: enabling variant + wild indicators + dual payout overlay (CardGames.Poker.Web)
#### 5a) Enable the variant in the “Create Table” page
- In [CreateTable.razor](CardGames.Poker.Web/Components/Pages/CreateTable.razor), the UI currently hard-codes:
  - `var isAvailable = game.Name == "Five Card Draw";`
- Replace that gating with a capability check from the API response:
  - either based on `game.Name` matching the Twos/Jacks/Axe metadata name,
  - or (better) extend `GetAvailablePokerGamesResponse` to include a stable `GameTypeCode`.
- Also inject the correct API client:
  - page currently injects `IFiveCardDrawApi FiveCardDrawApi` only; to create Twos/Jacks/Axe tables you’ll need `ITwosJacksManWithTheAxeApi` (exists in refit output: `TwosJacksManWithTheAxeCreateGameAsync` is present in the generated interface).
- Ensure “create table” calls the correct endpoint based on variant selection.

#### 5b) Show wild indicators during the hand and at showdown
- For showdown display, `ShowdownOverlay.razor` uses `ShowdownResult.PlayerHands` and renders cards via `<playing-card ...>`.
- Add wild indication in one of two ways:
  - **Card-level**: augment `CardPublicDto` (SignalR) with `IsWild` and render a small “WILD” badge/outline.
  - **Index-level**: add `WildCardIndexes` to `ShowdownPlayerResultDto` so the UI can mark certain positions as wild without changing card DTO.
- Also consider showing “evaluated best cards” (like `BaseballHand.EvaluatedBestCards`) vs original dealt cards; for player comprehension, show:
  - dealt cards with wild markers, and
  - a small “plays as …” strip if substitution changes ranks materially.

#### 5c) Dual payout showdown overlay
- Today `ShowdownOverlay` assumes a single primary winner (“takes it”) and a flat `Payouts` list.
- Update overlay to display:
  - “7s half” winners + chip amounts
  - “High hand half” winners + chip amounts
  - Totals still visible
- Note that `TablePlay.razor` currently reconstructs `PerformShowdownSuccessful` from `ShowdownPublicDto` in `UpdateShowdownFromPublicState()` and depends on `AmountWon` being valid. You’ll want to:
  - carry both sub-amounts into the reconstructed model (extend `PerformShowdownSuccessful` / `ShowdownPlayerHand` contracts if needed), or
  - keep `AmountWon` as total and add separate fields for the overlay.

---

### 6) Recommended tie + rounding rules (make them explicit)
To avoid “where did the odd chip go?” confusion, codify and document:

- **Per-pot settlement order**
  1. Compute `sevensPool = pot.Amount / 2` and `handPool = pot.Amount - sevensPool` (so the “hand half” gets the odd chip when pot is odd).
  2. If no sevens qualifiers, roll `sevensPool` into `handPool`.
  3. Split pools among winners:
     - `share = pool / winners.Count`
     - `remainder = pool % winners.Count`
     - Distribute remainder chips to winners in deterministic order.

- **Deterministic remainder order**
  - Recommended: ascending seat order starting left of dealer for that hand, limited to eligible winners.
  - Implementation detail: pass an ordered winner list into pot splitting (don’t rely on dictionary iteration).

- **Side pots**
  - Perform the same split independently for each `PotManager.Pots[i]` (main pot and each side pot).
  - A player can win:
    - sevens pool in a side pot they’re eligible for
    - but not in pots they didn’t cover (standard eligibility rules already baked into `Pot.EligiblePlayers`).

---

If you want, expand this plan by also naming the exact API PerformShowdown endpoint handler file for Twos/Jacks/Axe (it’s mapped under `V1.cs` as `MapPerformShowdown()`), since that handler will likely need to return the richer breakdown used by the overlay.

