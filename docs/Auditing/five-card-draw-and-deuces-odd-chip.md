# Walk leftover chips clockwise from the dealer in Five Card Draw and Deuces/Jacks/Axe

An LLM implements this plan. The table calls `FiveCardDrawPerformShowdown` and `TwosJacksManWithTheAxePerformShowdown`. Those handlers write `ChipStack`. Change only the order used when a pot or a pool is split. Leave side-pot eligibility, the sevens half, fold wins, and the other games in `docs/Auditing/odd-chip.md` as they are.

`docs/Auditing/odd-chip.md` steps 2 and 3 are this same order change and stop at a completion line. This plan is the one to implement for those two handlers. Do not also implement odd-chip steps 1 or 4.

`docs/Auditing/five-card-draw-side-pots.md` builds the pot rows and says not to edit Five Card Draw showdown. This plan is the showdown edit. Do not edit `CalculateSidePots`.

`docs/Auditing/twos-jacks-side-pots.md` deletes the whole-pot split and already pays each layer with `OddChipOrder`. If that rewrite has already removed the `Skip` loop, do not put the whole-pot split back. The tests below use one pot and no missing-eligibility case, so they still prove the order. If that rewrite has not landed, do not implement it here. Replace only the walk.

Do not edit `OddChipOrder.cs`.

## Outcome

When two or more players tie for an amount, each leftover chip goes to a different winner, one chip at a time, starting with the occupied seat clockwise from the dealer. The dealer is last among the tied winners of that amount.

`OddChipOrder` in `src/CardGames.Poker.Api/Features/Games/Common/Showdown/OddChipOrder.cs` is that rule. Hold'em, `ContributionLayerAwarder`, and the generic showdown already call it.

- `OrderClockwiseFromDealer` orders by `seat > dealerSeat ? 0 : 1`, then by seat. Seat 5 is before seat 0 when the dealer sits at seat 3.
- `Distribute` pays `amount / count` to each winner, then one extra chip to each of the first `amount % count` winners in that order.

A remainder of 1 is the proof. The first clockwise winner receives one more chip than the others. A pot of 5 split three ways pays 2, 2, and 1 to both "Carol then Alice then Bob" and "Alice then Carol then Bob", so it does not prove who is first. Use a remainder of 1.

The set of winners does not change.

- Five Card Draw still pays each pot to the eligible players whose `hand.Strength` equals the maximum among that pot's eligible hands. A zero-amount pot is still skipped. A pot with no contribution rows still falls back to every player in the hand.
- Deuces, Jacks, and Man with the Axe still splits `totalPot` into `totalPot / 2` for natural sevens and the rest for the high hand, and still rolls the sevens half into the high-hand half when nobody has a natural pair of sevens. Each half keeps its own walk. A player who wins both halves still has one combined `payouts` entry.

## The seats

Seats are not packed. `JoinGameCommandHandler` stores `SeatPosition = command.SeatIndex`. Insert the players in this order so primary-key order is not clockwise:

| Insert order | Player | Seat | Stack at showdown |
| --- | --- | --- | --- |
| 1 | Alice | 0 | 100 |
| 2 | Bob | 3 | 100, and `Game.DealerPosition` is 3 |
| 3 | Carol | 5 | 100 |

Clockwise tied order is Carol, Alice, Bob.

## What the live code pays

### Five Card Draw walks dictionary order

`src/CardGames.Poker.Api/Features/Games/FiveCardDraw/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` already adds one chip at a time:

```csharp
var potWinners = eligibleHands
    .Where(kvp => kvp.Value.hand.Strength == maxStrength)
    .Select(kvp => kvp.Key)
    .ToList();
var potPayoutPerWinner = pot.Amount / potWinners.Count;
var potRemainder = pot.Amount % potWinners.Count;
foreach (var winner in potWinners)
{
    var payout = potPayoutPerWinner;
    if (potRemainder > 0)
    {
        payout++;
        potRemainder--;
    }
}
```

`eligibleHands` is copied from `playerHandEvaluations`. That dictionary is filled by walking `playersInHand`, which is `game.GamePlayers` with no `OrderBy`. `DealerPosition` is not read. With the insert order above, the walk is Alice, Bob, Carol.

A main pot of 4 that all three tie pays Alice 2, Bob 1, Carol 1. Clockwise pays Carol 2, Alice 1, Bob 1.

A side pot of 5 that only Alice and Carol can win walks Alice then Carol and pays Alice 3, Carol 2. Clockwise from seat 3 pays Carol 3, Alice 2. Bob's side-pot amount is 0.

Totals today: Alice 5, Bob 1, Carol 3. After the fix: Carol 5, Alice 3, Bob 1.

### Deuces, Jacks, and Man with the Axe skips people

`src/CardGames.Poker.Api/Features/Games/TwosJacksManWithTheAxe/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` builds the walk with:

```csharp
for (var i = 1; i <= game.GamePlayers.Count; i++)
{
    var idx = (game.DealerPosition + i) % game.GamePlayers.Count;
    var player = game.GamePlayers.OrderBy(gp => gp.SeatPosition).Skip(idx).FirstOrDefault()
                 ?? game.GamePlayers.OrderBy(gp => gp.SeatPosition).First();
}
```

`Skip(idx)` skips people, not seat numbers. For this fixture, `(3 + 1) % 3` is 1, and `Skip(1)` on seats 0, 3, 5 is Bob. The list is Bob, Carol, Alice. Carol should be first.

The sevens pool and the high-hand pool each walk that list. Only the order is wrong.

No natural sevens, pot of 4. The sevens half rolls into the hand pool, so the hand pool is 4. The live walk pays Bob 2, Carol 1, Alice 1. Clockwise pays Carol 2, Alice 1, Bob 1.

Every player has a natural pair of sevens and the same high hand, pot of 7. Sevens pool is 3 and high-hand pool is 4. The live walk pays Bob 3, Carol 2, Alice 2 (sevens 1, 1, 1 in Bob, Carol, Alice order; high hand 2, 1, 1 in that same order). Clockwise pays Carol 3, Alice 2, Bob 2 (sevens 1, 1, 1; high hand 2, 1, 1).

## Hands

Duplicate faces across players are intentional. Neither handler checks that the deck is unique. The same faces force the same strength. Assert that equality in the unit test before asserting money. If the strengths differ, the fixture is wrong. Do not change the payout code to chop unequal hands.

Five Card Draw, and the Deuces/Jacks no-sevens case. Each player: Ah Kh Qh Jh Th. That is a royal flush with no deuce, jack, or king of diamonds, so nothing is wild and nobody has a seven. Construct `DrawHand` for Five Card Draw and `TwosJacksManWithTheAxeDrawHand` for Deuces/Jacks, and assert the three `Strength` values are equal. Also assert `HasNaturalPairOfSevens()` is false.

Deuces/Jacks sevens case. Each player: 7c 7d Ah Kh Qh. Assert all three `HasNaturalPairOfSevens()` values are true and all three `Strength` values are equal.

Store the cards as `CardLocation.Hole`, hand 1, `IsDiscarded` false, `DealOrder` 1 through 5. Five Card Draw's draw path uses every non-discarded card when `SupportsPositionalCards` is false. Set `Game.CurrentHandGameTypeCode` to `HandEvaluatorFactory.FiveCardDrawCode` (`"FIVECARDDRAW"`). Pass `new HandEvaluatorFactory()` into the Five Card Draw handler. Deuces/Jacks does not take an evaluator; it constructs `TwosJacksManWithTheAxeDrawHand` directly.

## Step 1. Five Card Draw

In the `foreach (var pot in orderedPots)` loop, after `eligibleHands` is built and `maxStrength` is known, replace the `potWinners` / `potPayoutPerWinner` / `potRemainder` construction. The file already imports `CardGames.Poker.Api.Features.Games.Common.Showdown`.

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
    if (payouts.TryGetValue(winner, out var existingPayout))
        payouts[winner] = existingPayout + payout;
    else
        payouts[winner] = payout;

    allWinners.Add(winner);

    var gp = eligibleHands[winner].gamePlayer;
    potPayoutsList.Add(new { playerId = gp.PlayerId.ToString(), playerName = winner, amount = payout });
}
```

`pot.WinnerPayouts` still records that pot's share, not the player's total across pots. `eligibleHands[potWinners[0]].hand` still supplies the win-reason hand type. `potWinners[0]` is now the first clockwise winner of that pot.

Leave the chip update, the `isAlreadyAwarded` guard, fold-win, and `SettleHandAsync(game, payouts, ...)` as they are. `payouts` values are the gross amounts added to `ChipStack`. A second call sees `IsAwarded` and must not add them again.

## Step 2. Deuces, Jacks, and Man with the Axe

Replace the `for (var i = 1; i <= game.GamePlayers.Count; i++)` loop. The ordered list is the evaluated players, clockwise from the dealer. The file already imports `CardGames.Poker.Api.Features.Games.Common.Showdown`.

```csharp
var orderedPlayers = OddChipOrder.OrderClockwiseFromDealer(
    playerHandEvaluations.Values.Select(value => value.gamePlayer),
    game.DealerPosition,
    gamePlayer => gamePlayer.SeatPosition);
var orderedPlayerNames = orderedPlayers.Select(gamePlayer => gamePlayer.Player.Name).ToList();
```

Leave sevens-winner detection, high-hand-winner detection, `totalPot / 2`, and the rollover when `sevensWinners.Count == 0` as they are.

Replace each pool's share/remainder loop with `Distribute`, still filtered through `orderedPlayerNames`:

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

Use the same shape for `handPool` and `highHandWinners`. `WinnerPayouts` still stores `amount`, `sevensAmount`, and `highHandAmount`. The same JSON is still copied onto every current-hand pot. Do not change that.

Leave the chip update inside `if (!isAlreadyAwarded)`, fold-win, and `SettleHandAsync` as they are.

## Step 3. Unit tests

Add:

- `src/Tests/CardGames.Poker.Tests/Api/Features/Games/FiveCardDraw/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`
- `src/Tests/CardGames.Poker.Tests/Api/Features/Games/TwosJacksManWithTheAxe/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`

Copy the in-memory `CardsDbContext` setup from `src/Tests/CardGames.Poker.Tests/Api/Features/Games/KingsAndLows/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`. Substitute `IHandHistoryRecorder` and `IHandSettlementService`. Five Card Draw also takes `IHandEvaluatorFactory`; pass `new HandEvaluatorFactory()`. `GameEventsTelemetry` is optional.

Seed phase `Showdown`, `CurrentHandNumber` 1, `DealerPosition` 3, `Status` active, `HasFolded` false, `ChipStack` 100, `StartingChips` 100. Add Alice, then Bob, then Carol, and `SaveChanges` in that order.

Capture the settlement substitute and assert it was received once with the gross dictionary, not the ending stack.

### Five Card Draw

One current-hand main pot, `PotOrder` 0, `PotType.Main`, amount 4, `IsAwarded` false. One side pot, `PotOrder` 1, `PotType.Side`, amount 5. Contribution rows:

- Main: Alice, Bob, and Carol, each `IsEligibleToWin` true.
- Side: Alice and Carol `IsEligibleToWin` true. Bob has a row with `IsEligibleToWin` false.

`Handle` returns `IsT0`. `Payouts` is Carol 5, Alice 3, Bob 1. Stacks are Carol 105, Alice 103, Bob 101. The three stacks sum to 309.

The main pot's `WinnerPayouts` is Carol 2, Alice 1, Bob 1. The side pot's `WinnerPayouts` is Carol 3, Alice 2, and does not contain Bob. Neither JSON contains the cross-pot totals 5 and 3.

A second `Handle` leaves the three stacks unchanged.

### Deuces, Jacks, and Man with the Axe

1. No sevens, one main pot of 4, the royal-flush cards. `Payouts` is Carol 2, Alice 1, Bob 1. Stacks are Carol 102, Alice 101, Bob 101. The awarded pot's `WinnerPayouts` has `sevensAmount` 0 and `highHandAmount` 2 for Carol, 1 for Alice, and 1 for Bob. `SevensPoolRolledOver` is true. A second `Handle` leaves the stacks unchanged. Settlement was received once with those three gross amounts.

2. Natural sevens, one main pot of 7, the sevens cards. `Payouts` is Carol 3, Alice 2, Bob 2. Stacks are Carol 103, Alice 102, Bob 102. The three stacks sum to 307. `WinnerPayouts` has Carol `sevensAmount` 1 and `highHandAmount` 2, Alice 1 and 1, Bob 1 and 1. `SevensPoolRolledOver` is false.

Run:

```bash
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~FiveCardDraw.v1.Commands.PerformShowdown|FullyQualifiedName~TwosJacksManWithTheAxe.v1.Commands.PerformShowdown"
```

The Five Card Draw test fails on the current handler: Alice's stack becomes 105 and Carol's becomes 103. The no-sevens Deuces/Jacks test fails because Bob's stack becomes 102 and Carol's becomes 101.

## Step 4. Integration tests

Add:

- `src/Tests/CardGames.IntegrationTests/Games/FiveCardDraw/OddChipShowdownTests.cs`
- `src/Tests/CardGames.IntegrationTests/Games/TwosJacksManWithTheAxe/OddChipShowdownTests.cs`

Both subclass `IntegrationTestBase`.

Use `DatabaseSeeder.CreateCompleteGameSetupAsync` with `"FIVECARDDRAW"` or `"TWOSJACKSMANWITHTHEAXE"`, 3 players, `startingChips: 100`, `ante: 0`. The seeder names them `Player 1`, `Player 2`, and `Player 3` at seats 0, 1, and 2, with `DealerPosition` 0, and inserts them in that order. Rename them Alice, Bob, and Carol, move the seats to 0, 3, and 5, and set `DealerPosition` to 3. Set phase `Showdown`, `CurrentHandNumber` 1, `CurrentHandGameTypeCode` to the same code, and `ChipStack` 100. Delete any dealt cards. Insert the five hole cards and the pots from the unit tests, including the Five Card Draw contribution rows. Do not call `StartHand`, `CollectAntes`, or `DealHands`. Those deal a random hand.

`Mediator.Send` the command from that game's `PerformShowdown` namespace. Reload with `GetFreshDbContext` before every assertion.

Five Card Draw: stacks Alice 103, Bob 101, Carol 105. Main-pot JSON is Carol 2, Alice 1, Bob 1. Side-pot JSON is Carol 3, Alice 2, with no Bob. A second command leaves the stacks there.

Deuces/Jacks, two facts, same seats:

- Pot of 4, royal flushes. Stacks Alice 101, Bob 101, Carol 102.
- Pot of 7, natural sevens. Stacks Alice 102, Bob 102, Carol 103. The pot JSON has Carol's `highHandAmount` 2 and Bob's `highHandAmount` 1.

Run:

```bash
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~FiveCardDraw.OddChipShowdownTests|FullyQualifiedName~TwosJacksManWithTheAxe.OddChipShowdownTests"
```

Both tests call the handlers `TablePlay.TryLoadShowdownAsync` calls. A unit test that only calls `OddChipOrder.Distribute` does not prove these handlers.

## Out of scope

Do not change Kings and Lows, Hold the Baseball, Hold'em, `ContributionLayerAwarder`, the generic showdown, or `OddChipOrder`. Do not change `CalculateSidePots` in either game. Do not change who is eligible, how the sevens half is sized, or the rollover when nobody has a natural pair of sevens. Do not change fold-win or dealer movement.
