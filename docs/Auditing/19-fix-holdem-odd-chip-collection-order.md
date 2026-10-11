# Fix: Hold’em odd chip follows collection order

Implement this document only. The live showdown for Texas Hold’em and Red River is `HoldEm/.../PerformShowdownCommandHandler`. Red River calls it through `RedRiverApiClientWrapper.HoldEmPerformShowdownAsync`. Do not change Generic showdown, stud, draw, Kings and Lows, or Twos/Jacks.

## Defect

`playersInHand` is `game.GamePlayers.Where(!HasFolded && (Active || IsAllIn)).ToList()` with no `OrderBy`. The handler fills `playerHandEvaluations` by walking that list. `winners` keeps that same order. On a tie it does this:

```csharp
var payoutPerWinner = totalPot / winners.Count;
var remainder = totalPot % winners.Count;
foreach (var winner in winners) payouts[winner] = payoutPerWinner;
if (remainder > 0 && winners.Count > 0) payouts[winners[0]] += remainder;
```

`winners[0]` is whoever was inserted into `GamePlayers` first among the tied players. The whole remainder goes to that one player. `docs/SidePots.md` §4.3 and §10.5 deal the remainder one chip at a time, starting at the first winner clockwise from `DealerPosition`.

`DealerPosition` is a seat number. `MoveDealer` runs after this block and sets it to the next occupied seat. The button for the odd chip is the value before that call.

A single winner still receives `totalPot`, because `totalPot % 1` is 0. A fold win is the earlier `playersInHand.Count == 1` block and does not use this split.

## Worked hand

Phase `Showdown`. `CurrentHandNumber` 1. `DealerPosition` 2. One unawarded pot, `PotOrder` 0, `Amount` 100. Starting `ChipStack` 1000 for each player. `GamePlayers` is filled Alice, then Bob, then Carol, so today’s `winners[0]` is Alice.

Board, in deal order: As, Ks, Qs, Js, Ts. That royal is the best five for every player.

| Seat | Player | Hole cards |
| --- | --- | --- |
| 0 | Alice | 2c 3d |
| 2 | Bob | 4c 5d |
| 5 | Carol | 6c 7d |

All three tie. Clockwise from seat 2 the winner seats are 5, then 0, then 2. Carol, Alice, Bob.

`100 / 3` is 33, remainder 1.

| Player | Today | After this fix | Stack after |
| --- | --- | --- | --- |
| Carol | 33 | 34 | 1034 |
| Alice | 34 | 33 | 1033 |
| Bob | 33 | 33 | 1033 |

The payouts sum to 100. `DealerPosition` after `MoveDealer` is 5, the next occupied seat after 2. The payout used seat 2.

### Remainder 2

Same seats, same cards, pot 101. `101 / 3` is 33, remainder 2.

| Player | Payout | Stack after |
| --- | --- | --- |
| Carol | 34 | 1034 |
| Alice | 34 | 1034 |
| Bob | 33 | 1033 |

Sum 101. Carol does not receive 35, and Alice does not receive 35.

### Collection order would pick the button

Same board. Only Alice and Bob remain; Carol has `HasFolded` true. Insert Bob into `GamePlayers` before Alice. Pot 101. `DealerPosition` is still 2.

Today `winners[0]` is Bob, so Bob receives 51 and Alice 50.

Clockwise from seat 2, neither winner sits above 2, so the walk wraps to the lowest winner seat. Alice (seat 0) receives 51. Bob receives 50. Stacks 1051 and 1050. Sum 101.

## The payout

Replace only the split at the `payoutPerWinner` / `winners[0] += remainder` lines. Leave the fold-win block alone. Leave `MoveDealer` where it is, after the stacks are updated.

Order the tied names before dealing:

```csharp
var orderedWinners = winners
	.OrderBy(name => playerHandEvaluations[name].gamePlayer.SeatPosition > game.DealerPosition ? 0 : 1)
	.ThenBy(name => playerHandEvaluations[name].gamePlayer.SeatPosition)
	.ToList();
```

Seats greater than `DealerPosition` come first, ascending. The other winner seats follow, ascending. Empty seats are absent from `winners`, so they are skipped. A winner sitting on the button is last among the winners who wrap.

Deal that pot one chip at a time:

```csharp
var payouts = new Dictionary<string, int>();
var share = totalPot / orderedWinners.Count;
var remainder = totalPot % orderedWinners.Count;
foreach (var winner in orderedWinners)
{
	var payout = share;
	if (remainder > 0)
	{
		payout++;
		remainder--;
	}

	payouts[winner] = payout;
}
```

`remainder` is smaller than the winner count, so one pass finishes it. Do not add the whole remainder to `orderedWinners[0]`. Do not sort `playersInHand`. Do not use `(DealerPosition + i) % GamePlayers.Count`.

The `winReason` line may keep using `winners[0]`. Every tied player has the same `HoldemHand.Type`. `WinnerPayouts`, `ChipStack`, the response `AmountWon`, and `SettleHandAsync` already read `payouts`. They pick up the new amounts without a formula change.

`Dictionary<string, int>` stays the default comparer. Insert names in `orderedWinners` order.

## If the side-pot award is already in this handler

`docs/Auditing/07-fix-holdem-side-pots-and-uncalled-bets.md` is a different change. It is not in the handler today. If that per-pot loop is present when you edit, do not put the layers back into one `totalPot` before the odd chip. For each pot, build that pot’s winner names the way that plan already does, run the same `OrderBy` / `ThenBy` on those names, and deal that pot’s `Amount` with the loop above. Add the result into the hand dictionary. A remainder on the main pot is not dealt again on the side pot.

If that loop is absent, change the single `totalPot` split as written. Do not add side pots in this change.

## Tests

Add these facts to `PerformShowdownCommandHandlerTests`. Use `CreateSut`. Phase `Showdown`. One pot, `IsAwarded` false, `HandNumber` 1. Five community cards, `Location` `Community`, `GamePlayerId` null, `HandNumber` 1, deal order 1 through 5: As, Ks, Qs, Js, Ts. Two hole cards each, `Location` `Hole`.

1. `Handle_TiedShowdown_OddChipGoesToSeatAfterButton`. The worked hand, pot 100, `GamePlayers` inserted Alice, Bob, Carol. `DealerPosition` 2. Response `Payouts` and each `AmountWon`: Carol 34, Alice 33, Bob 33. Reloaded stacks 1034, 1033, 1033. Reloaded `DealerPosition` is 5. Sum of `Payouts` is 100.

2. `Handle_TiedShowdown_SecondOddChipGoesToNextSeat`. Pot 101. Carol 34, Alice 34, Bob 33. Stacks 1034, 1034, 1033. Sum of `Payouts` is 101.

3. `Handle_TiedShowdown_WrapsPastButtonInsteadOfCollectionOrder`. Carol folded. `GamePlayers` inserted Bob, then Alice. Pot 101. `DealerPosition` 2. Alice 51, Bob 50. Stacks 1051 and 1050. Bob is first in the collection and does not receive 51.

`Handle_WhenOnlyOnePlayerRemains_ReturnsFoldWinWithoutHoleCards` stays a fold win. Do not require hole-card evaluation there.

## Do not change

- The fold-win block, including its payout of `totalPot` to the last player.
- Who wins. This only orders an existing tie.
- `MoveDealer`. Read `DealerPosition` before it runs.
- `HandSettlementService`. The dictionary it receives changes by 1 for the odd chip.
- Generic `AwardAmount` and every other game’s showdown.
- Side-pot construction, uncalled bets, and `HoldEmPotLayers`, unless that award loop is already in this handler. Then only the odd-chip walk inside it changes.
- `docs/Auditing/15-fix-odd-chip-seat-order.md`. That plan covers other games. Do not implement it here.

## Done when

1. The pot-100 tie pays Carol 34, Alice 33, Bob 33, and the button stored for the next hand is seat 5.
2. The pot-101 tie pays Carol 34, Alice 34, Bob 33, and those payouts sum to 101.
3. With Carol folded and Bob inserted before Alice, Alice receives the one odd chip from pot 101.
4. `dotnet test src/Tests/CardGames.Poker.Tests --filter "FullyQualifiedName~HoldEm.v1.Commands.PerformShowdown"` passes.
