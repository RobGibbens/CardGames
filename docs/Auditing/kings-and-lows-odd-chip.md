# Split Kings and Lows leftover chips clockwise from the dealer

An LLM implements this plan. The table calls `KingsAndLowsPerformShowdown`. That handler writes `ChipStack`. Change only the contested multi-winner payout in `PerformShowdownCommandHandler`. Leave pot matching amounts, player-versus-deck, fold wins, and the other games in `docs/Auditing/odd-chip.md` as they are.

`docs/Auditing/odd-chip.md` step 1 is this same edit and stops at a one-line completion note. This plan is the one to implement. Do not also implement odd-chip steps 2 through 4. If step 1 has already replaced the lump, do not replace it again. Add the tests below either way.

## Outcome

When two or more players tie the current hand's pot, each leftover chip goes to a different winner, one chip at a time, starting with the occupied seat clockwise from the dealer. The dealer is last among the tied winners.

`OddChipOrder` in `src/CardGames.Poker.Api/Features/Games/Common/Showdown/OddChipOrder.cs` is that rule. Hold'em, `ContributionLayerAwarder`, and the generic showdown already call it. Do not edit `OddChipOrder.cs`.

- `OrderClockwiseFromDealer` orders by `seat > dealerSeat ? 0 : 1`, then by seat. Seat 5 is before seat 0 when the dealer sits at seat 3.
- `Distribute` pays `amount / count` to each winner, then one extra chip to each of the first `amount % count` winners in that order.

The set of winners does not change. A winner is still every evaluated player whose `KingsAndLowsDrawHand.Strength` equals the maximum. Pot matching still charges each non-winner `Math.Min(totalPot, gamePlayer.ChipStack)` into the next hand's pot. The match uses the whole pot, not that loser's share of a split.

## The hand that fails today

`PerformShowdownCommandHandler` builds `winners` from `playerHandEvaluations` with no seat sort. `playerHandEvaluations` is filled from `playersInHand`, and `playersInHand` is `game.GamePlayers` filtered with no `OrderBy`. It then does:

```csharp
var payoutPerWinner = totalPot / winners.Count;
var remainder = totalPot % winners.Count;
foreach (var winner in winners) payouts[winner] = payoutPerWinner;
if (remainder > 0 && winners.Count > 0) payouts[winners[0]] += remainder;
```

The comment above that line says the first winner is closest to the dealer's left. `DealerPosition` is not read. The entire remainder is added to one player. A pot of 5 split three ways pays 3, 1, and 1. `OddChipOrder.Distribute(5, three winners)` pays 2, 2, and 1.

Seats are not packed. `JoinGameCommandHandler` stores `SeatPosition = command.SeatIndex`. This fixture has a gap:

| Player | Seat | Starting stack at showdown |
| --- | --- | --- |
| Alice | 0 | 100 |
| Bob | 3 | 100, and `Game.DealerPosition` is 3 |
| Carol | 5 | 100 |

Clockwise tied order is Carol, Alice, Bob.

The current-hand pot is already 5. Showdown adds the payout to `ChipStack`. It does not subtract the pot again. After a three-way tie the stacks must be Carol 102, Alice 102, Bob 101. The live code makes one of those three stacks 103 and the other two 101. Which player receives 103 is whichever tied name the unordered list returns first.

## Hands that tie

Kings and Lows makes every king wild, and it also makes every card of the lowest non-king rank wild. Do not use kings or aces in the tie. One copy of the lowest rank keeps a single wild, and the same ranks in different suits evaluate to the same `Strength`.

Three-way tie, one hand each:

- Alice: Qs Jd 9h 8c 7s
- Bob: Qh Jc 9d 8s 7h
- Carol: Qd Js 9c 8h 7d

The 7 is the only wild. Each hand's best use of that wild is the same queen-high straight. The test must construct `KingsAndLowsDrawHand` for these three lists and assert the three `Strength` values are equal before it asserts money. If they are not equal, the fixture is wrong. Do not change the payout code to make unequal hands chop.

Two winners and one loser, for the pot-match guard:

- Alice: Qs Jd 9h 8c 7s
- Carol: Qd Js 9c 8h 7d
- Bob: 2s 3h 4d 5c 9h

Bob's only wild is the deuce. That hand is one pair and loses to the straight. The test must assert `Bob.Strength < Alice.Strength` and `Alice.Strength == Carol.Strength` before it asserts money.

## Step 1. Pay the pot with OddChipOrder

In `src/CardGames.Poker.Api/Features/Games/KingsAndLows/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`, replace the block that sets `payoutPerWinner` and `remainder`. The file already imports `CardGames.Poker.Api.Features.Games.Common.Showdown`.

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

Keep `losers` as `playerHandEvaluations.Keys.Except(winners)`. That set is unchanged, so the loop that follows still subtracts `Math.Min(totalPot, gamePlayer.ChipStack)` from each loser and still creates the next hand's pot for `totalMatched`. Do not pass a winner's share into that `Math.Min`.

`winners[0]` is now the first clockwise winner. It remains the name used for the split-pot `WinReason`. Every tied winner has the same hand type, so the text stays `Split pot - {that type}`.

Leave the chip update, the `isAlreadyAwarded` guard, `SettleHandAsync(game, payouts, ...)`, and `WinnerPayouts` as they are. `payouts` values are the gross amounts added to `ChipStack`. A second call sees `IsAwarded` and must not add them again.

Do not change the player-versus-deck return earlier in the method, the fold-win return, or `MoveDealer`. `DrawCardsCommandHandler.PerformShowdownAndSetupPotMatching` has no callers. Leave it.

## Step 2. Unit tests

Extend `src/Tests/CardGames.Poker.Tests/Api/Features/Games/KingsAndLows/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`. It already builds an EF Core in-memory `CardsDbContext` and substitutes `IHandHistoryRecorder` and `IHandSettlementService`.

Add a seed helper that creates three players named Alice, Bob, and Carol at seats 0, 3, and 5, `DealerPosition` 3, phase `Showdown`, `CurrentHandNumber` 1, `Status` active, `HasFolded` false, and `ChipStack` 100. Add one current-hand `Pot` with `Amount` 5, `PotOrder` 0, `PotType.Main`, and `IsAwarded` false. Give each showdown player five `GameCard` rows for hand 1, `Location = CardLocation.Hand`, `IsDiscarded` false, `DealOrder` 1 through 5. The handler skips a player with fewer than five cards.

Capture the settlement substitute:

```csharp
var settlement = Substitute.For<IHandSettlementService>();
```

Pass it to the handler. After a successful showdown, `settlement.Received(1).SettleHandAsync(Arg.Any<Game>(), Arg.Is<Dictionary<string, int>>(p => ...), Arg.Any<CancellationToken>())`. The dictionary is the gross payout, not the stack.

Tests:

1. Three-way tie, cards from the section above. `Payouts` is Carol 2, Alice 2, Bob 1. Stacks are Carol 102, Alice 102, Bob 101. The sum of the three stacks is 305. No pot exists for hand 2. The hand-1 pot is awarded, and its `WinnerPayouts` JSON contains `"amount":2` for Carol and Alice and `"amount":1` for Bob. It does not contain `"amount":3`. `Winners` in the response is Carol, then Alice, then Bob. A second `Handle` leaves the three stacks unchanged. Settlement was received once, with those three gross amounts.

2. Alice and Carol tie, Bob holds the losing hand from the section above. `Payouts` is Carol 3, Alice 2. Bob is absent from `Payouts`. Stacks are Carol 103, Alice 102, Bob 95. Bob's 95 is 100 minus the full pot of 5, not minus a winner's share. Hand 2 has one main pot of 5. Hand 1's awarded amount is still 5. The sum of the three stacks plus the hand-2 pot is 305.

3. One winner. Alice holds Qs Jd 9h 8c 7s. Bob holds 2s 3h 4d 5c 9h. Carol is folded and has no cards, so she is not evaluated. Pot 5. Alice's stack becomes 105. Bob's stack becomes 95. Carol's stack stays 100. Hand 2's pot is 5, from Bob only. This guards the one-winner path against a split that pays Alice 2 or 3.

Run:

```bash
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter PerformShowdownCommandHandlerTests
```

The namespace filter is shared with other games. Run it from the Kings and Lows test file's full name if the filter is too wide:

```bash
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~KingsAndLows.v1.Commands.PerformShowdown"
```

Test 1 fails on the current handler: one stack increases by 3. Test 2 fails because the extra chip is not chosen from seat 5.

## Step 3. Integration test

Add `src/Tests/CardGames.IntegrationTests/Games/KingsAndLows/OddChipShowdownTests.cs`, subclassing `IntegrationTestBase`.

Use `DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 3, startingChips: 100, ante: 0)`. The seeder names them `Player 1`, `Player 2`, and `Player 3` at seats 0, 1, and 2, with `DealerPosition` 0. Rename them Alice, Bob, and Carol, move the seats to 0, 3, and 5, and set `DealerPosition` to 3. Set phase `Showdown`, `CurrentHandNumber` 1, and `ChipStack` 100. Replace any dealt cards with the five cards for the scenario, hand 1, not discarded, `CardLocation.Hand`. Insert the current-hand pot of 5 if `StartHand` is not used. Do not call `StartHand` or `DropOrStay`. Those commands deal a random hand and would make the tie accidental. `Mediator.Send` the Kings and Lows `PerformShowdownCommand`.

Reload with `GetFreshDbContext` before every assertion.

1. Three-way tie. Stacks are Alice 102, Bob 101, Carol 102. Their sum is 305. The hand-1 pot is awarded. No hand-2 pot exists. A second `PerformShowdownCommand` leaves the stacks at those values.

2. Alice and Carol tie, Bob loses. Stacks are Alice 102, Bob 95, Carol 103. Hand 2 has one unawarded main pot of amount 5. The three stacks plus that pot sum to 305.

Both tests call the handler that `TablePlay.TryLoadShowdownAsync` calls for Kings and Lows. A unit test that only calls `OddChipOrder.Distribute` does not prove this handler.

Run:

```bash
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter OddChipShowdownTests
```

## Out of scope

Do not change Five Card Draw, Deuces/Jacks/Axe, Hold the Baseball, or `OddChipOrder`. Do not change how much a loser matches. Do not change the player-versus-deck payout, where one player either takes the pot or pays it into the next hand.
