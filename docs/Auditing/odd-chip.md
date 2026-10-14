# Fix odd-chip splits

An LLM implements this plan. Award every tied pot through `OddChipOrder`. Leave side-pot eligibility, uncalled-bet refunds, and Kings and Lows pot matching amounts as they are.

## Outcome

When two or more players tie for a pot, each leftover chip goes to a different winner, one chip at a time, starting with the occupied seat clockwise from the dealer.

`OddChipOrder` in `src/CardGames.Poker.Api/Features/Games/Common/Showdown/OddChipOrder.cs` is that rule:

- `OrderClockwiseFromDealer` orders by `seat > dealerSeat ? 0 : 1`, then by seat. Seat 5 is before seat 0 when the dealer sits at seat 3. The dealer is last among the tied winners.
- `Distribute` pays `amount / count` to each winner, then one extra chip to each of the first `amount % count` winners in that order.

The same order already pays Hold'em (`PerformShowdownCommandHandler.AwardPot`), `ContributionLayerAwarder.AwardPot`, and `Generic.PerformShowdownCommandHandler.AwardAmount`. This plan makes the four handlers below call `OddChipOrder` too. Do not edit `OddChipOrder`, Hold'em, `ContributionLayerAwarder`, or the generic handler.

`docs/SidePots.md` section 10.5 describes this same seat order. The code in `OddChipOrder` is the source of truth.

## The hands that fail today

Seats are not packed. `JoinGameCommandHandler` stores `SeatPosition = command.SeatIndex`, so gaps are normal. These three players are the fixture for every test below:

| Player | Seat | Role |
| --- | --- | --- |
| Alice | 0 | first occupied seat |
| Bob | 3 | dealer (`Game.DealerPosition == 3`) |
| Carol | 5 | first seat clockwise from Bob |

Clockwise tied order is Carol, Alice, Bob.

### Kings and Lows lumps the remainder

`src/CardGames.Poker.Api/Features/Games/KingsAndLows/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` builds `winners` from `playerHandEvaluations` with no seat sort, then adds the entire remainder to `payouts[winners[0]]`.

Pot of 5, three-way tie. `Distribute(5, [Carol, Alice, Bob])` is 2, 2, 1. The live code pays 3, 1, 1, and the player who receives 3 is whichever tied name the unordered list returns first.

The table calls this handler (`TablePlay.TryLoadShowdownAsync` → `KingsAndLowsPerformShowdown`).

### Five Card Draw walks the wrong list

`src/CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` already gives one leftover chip at a time, but `potWinners` is the dictionary order of `eligibleHands`. That order is the materialization of `game.GamePlayers`, not clockwise from `game.DealerPosition`.

Each pot is awarded on its own. A side pot orders only the players eligible for that pot.

The table calls this handler (`FiveCardDrawPerformShowdown`).

### Deuces, Jacks, and Man with the Axe indexes player count

`src/CardGames.Poker.Api/Features/Games/TwosJacksManWithTheAxe/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` builds `orderedPlayerNames` with:

```csharp
var idx = (game.DealerPosition + i) % game.GamePlayers.Count;
var player = game.GamePlayers.OrderBy(gp => gp.SeatPosition).Skip(idx).FirstOrDefault()
```

`Skip(idx)` skips people, not seat numbers. For the fixture, `(3 + 1) % 3` is 1, and `Skip(1)` on seats 0, 3, 5 is Bob, the dealer. The list becomes Bob, Carol, Alice. Carol should be first.

The sevens pool and the high-hand pool are separate amounts. Each pool keeps its own one-chip walk. Only the order is wrong. A pot of 5 with no natural sevens rolls the sevens half into the hand pool (`sevensPool` 0, `handPool` 5) and then walks that wrong list, so Bob receives 2, Carol 2, and Alice 1.

The table calls this handler.

### Hold the Baseball's dedicated handler lumps the remainder

`src/CardGames.Poker.Api/Features/Games/HoldTheBaseball/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` copies the Kings and Lows lump: `payouts[winners[0]] += remainder`.

`TablePlay` calls `GenericPerformShowdown` for Hold the Baseball, and that path already uses `OddChipOrder`. The dedicated endpoint is still mapped and writes `ChipStack` when it runs first (`isAlreadyAwarded` makes the first caller win). Both doors pay the same odd chips after this fix. Leave the table on the generic endpoint.

## Step 1. Kings and Lows

In the contested-showdown block that currently sets `payoutPerWinner` and `remainder`, replace the payout construction with `OddChipOrder`. Keep the winner set identical: still the players whose `hand.Strength` equals `maxStrength`.

```csharp
var orderedWinners = OddChipOrder.OrderClockwiseFromDealer(
    playerHandEvaluations.Where(kvp => kvp.Value.hand.Strength == maxStrength),
    game.DealerPosition,
    kvp => kvp.Value.gamePlayer.SeatPosition);
var shares = OddChipOrder.Distribute(totalPot, orderedWinners);
var winners = orderedWinners.Select(kvp => kvp.Key).ToList();
var payouts = new Dictionary<string, int>();
for (var index = 0; index < winners.Count; index++)
    payouts[winners[index]] = shares[index];
```

`losers` stays `playerHandEvaluations.Keys.Except(winners)`. The name set is unchanged, so pot matching still charges the same losers the same `Math.Min(totalPot, gamePlayer.ChipStack)`.

`winners[0]` remains the source of the split-pot hand-type text. Every tied winner has the same strength, so the text stays a split of that hand type. The first name is now the clockwise winner.

The file already imports `CardGames.Poker.Api.Features.Games.Common.Showdown`.

`DrawCardsCommandHandler.PerformShowdownAndSetupPotMatching` has no callers. Leave it.

Completion: the fixture pot of 5 pays Carol 2, Alice 2, Bob 1, and the three `ChipStack` values rise by those amounts.

## Step 2. Five Card Draw

Inside the `foreach (var pot in orderedPots)` loop, after `eligibleHands` is built, order the tied eligible players and pay with `Distribute`. Keep the existing skip of a zero-amount pot and the existing fallback that fills `eligiblePlayerIds` from `playersInHand` when the pot has no contribution rows.

```csharp
var orderedWinners = OddChipOrder.OrderClockwiseFromDealer(
    eligibleHands.Where(kvp => kvp.Value.hand.Strength == maxStrength),
    game.DealerPosition,
    kvp => kvp.Value.gamePlayer.SeatPosition);
var shares = OddChipOrder.Distribute(pot.Amount, orderedWinners);
var potWinners = orderedWinners.Select(kvp => kvp.Key).ToList();

var potPayoutsList = new List<object>();
for (var index = 0; index < potWinners.Count; index++)
{
    var winner = potWinners[index];
    var payout = shares[index];
    // existing accumulation into payouts, allWinners, and potPayoutsList
}
```

`pot.WinnerPayouts` still records that pot's share, not the player's total across pots. A later side pot adds to `payouts[winner]` the same way the current loop does.

`eligibleHands[potWinners[0]].hand` still supplies the win-reason hand type.

The file already imports `CardGames.Poker.Api.Features.Games.Common.Showdown`.

Completion: with the fixture, a main pot of 5 that all three can win pays Carol 2, Alice 2, Bob 1. A side pot of 5 that only Alice and Carol can win pays Carol 3 and Alice 2. Bob's side-pot amount is 0 and his name is absent from that pot's `WinnerPayouts`. Totals are Carol 5, Alice 4, Bob 1.

## Step 3. Deuces, Jacks, and Man with the Axe

Replace the `for (var i = 1; i <= game.GamePlayers.Count; i++)` loop. The ordered list is the evaluated players, clockwise from the dealer:

```csharp
var orderedPlayers = OddChipOrder.OrderClockwiseFromDealer(
    playerHandEvaluations.Values.Select(value => value.gamePlayer),
    game.DealerPosition,
    gamePlayer => gamePlayer.SeatPosition);
var orderedPlayerNames = orderedPlayers.Select(gamePlayer => gamePlayer.Player.Name).ToList();
```

Leave the sevens-winner and high-hand-winner detection, the `totalPot / 2` split, and the rollover when `sevensWinners.Count == 0` as they are.

Replace each pool's share/remainder loop with `Distribute`, still filtered through `orderedPlayerNames` so the pool order stays clockwise:

```csharp
var sevensWinnersList = orderedPlayerNames.Where(name => sevensWinners.Contains(name)).ToList();
var sevensShares = OddChipOrder.Distribute(sevensPool, sevensWinnersList);
for (var index = 0; index < sevensWinnersList.Count; index++)
{
    var winner = sevensWinnersList[index];
    var payout = sevensShares[index];
    sevensPayouts[winner] = payout;
    payouts[winner] = payouts.GetValueOrDefault(winner, 0) + payout;
}
```

Use the same shape for `handPool` and `highHandWinners`. A player who wins both pools still has one combined `payouts` entry. `WinnerPayouts` still stores `sevensAmount` and `highHandAmount` separately.

The file already imports `CardGames.Poker.Api.Features.Games.Common.Showdown`.

Completion: the fixture with no natural sevens and a pot of 5 pays Carol 2, Alice 2, Bob 1. The fixture where every player has a natural pair of sevens and the same high hand, pot of 7, pays Carol 3, Alice 2, Bob 2 (sevens pool 3 pays 1, 1, 1; high-hand pool 4 pays 2, 1, 1).

## Step 4. Hold the Baseball dedicated handler

Replace the `payoutPerWinner` / `remainder` block with the same `OddChipOrder` construction as Step 1. Use this handler's tuple fields (`hand` and `gamePlayer`). The file already imports `CardGames.Poker.Api.Features.Games.Common.Showdown`.

Do not switch this handler onto `ContributionLayerAwarder`. It still awards `totalPot` as one amount.

Completion: the fixture pot of 5 pays Carol 2, Alice 2, Bob 1 through this handler.

## Step 5. Tests

Add one test class per handler, beside the Hold'em showdown tests in style:

- `src/Tests/CardGames.Poker.Tests/Api/Features/Games/KingsAndLows/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`
- `src/Tests/CardGames.Poker.Tests/Api/Features/Games/FiveCardDraw/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`
- `src/Tests/CardGames.Poker.Tests/Api/Features/Games/TwosJacksManWithTheAxe/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`
- `src/Tests/CardGames.Poker.Tests/Api/Features/Games/HoldTheBaseball/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`

Copy the in-memory `CardsDbContext` setup from `src/Tests/CardGames.Poker.Tests/Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs` (`CreateContext`, `CreatePlayer`, `CreateGamePlayer`, `CreateHoleCard`). Substitute `IHandHistoryRecorder` and `IHandSettlementService`. Five Card Draw also takes `IHandEvaluatorFactory`; pass `new HandEvaluatorFactory()` and set `Game.CurrentHandGameTypeCode` to `HandEvaluatorFactory.FiveCardDrawCode` so the handler builds a `DrawHand`.

Seed:

- Phase `Showdown`, `CurrentHandNumber` 1, `DealerPosition` 3.
- Alice seat 0, Bob seat 3, Carol seat 5. Insert GamePlayers in that order (Alice, then Bob, then Carol) so insertion order is not clockwise.
- `Status = Active`, `HasFolded = false`, `ChipStack = 100`, `StartingChips = 100`.
- One current-hand main pot, `IsAwarded = false`, `PotOrder = 0`.
- Five `CardLocation.Hole` cards per player for Kings and Lows, Five Card Draw, and Deuces/Jacks. Give every player the same faces: hearts ace, king, queen, jack, ten. Duplicate faces across players are intentional in this test; the handler does not check the deck, and equal faces force equal strength. For the Deuces/Jacks sevens fact, use seven of clubs, seven of diamonds, ace of hearts, king of hearts, queen of hearts for every player.
- Hold the Baseball: two hole cards per player, both ace of hearts and ace of diamonds, plus one shared board of five `CardLocation.Community` cards with `GamePlayerId` null: king, queen, jack, ten, and deuce of clubs. Avoid treys and nines. Include a `GameType` row because this handler includes `Game.GameType`.

Assert `result.IsT0`, then `Payouts["Carol"]`, `Payouts["Alice"]`, and `Payouts["Bob"]` equal the amounts in the step completion lines. Reload `GamePlayers` and assert `ChipStack` is 100 plus that payout. Assert the sum of the three payouts equals the pot amount (10 when Five Card Draw has both pots).

Five Card Draw adds `PotContribution` rows. Main pot: all three `IsEligibleToWin = true`. Side pot: Alice and Carol only, `PotOrder = 1`, `PotType = Side`, amount 5. Assert each pot's `WinnerPayouts` contains that pot's amount for that player.

A second fact on the Kings and Lows class: one winner (fold the other two, or give only Alice a hand) receives the whole pot of 5. `Distribute` of a one-winner list returns the whole amount.

Run:

```bash
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~PerformShowdownCommandHandlerTests|FullyQualifiedName~OddChipOrderTests"
```

Completion: that filter passes, including the existing Hold'em `PerformShowdownCommandHandlerTests` and `OddChipOrderTests`.

## Out of scope

- Hole-card visibility and `TableStateBuilder`.
- `OddChipOrder` itself, Hold'em `AwardPot`, `ContributionLayerAwarder`, and generic `AwardAmount`.
- Kings and Lows loser matching, the next-hand matched pot, and `DrawCardsCommandHandler.PerformShowdownAndSetupPotMatching`.
- Five Card Draw side-pot creation in `ProcessBettingAction`. This plan only orders the winners of pots that already exist.
- Moving Hold the Baseball's table call off `GenericPerformShowdown`.
- Dealer-button movement, hand history, and cashier settlement. Those still run; the tests only assert payouts and stacks.

## Done

Carol, Alice, and Bob, with Bob dealing from seat 3, receive leftover chips in that order from Kings and Lows, Five Card Draw (per pot, among players eligible for that pot), Deuces/Jacks (per pool), and the Hold the Baseball dedicated handler. A pot of 5 split three ways pays 2, 2, and 1. No winner receives the whole remainder.
