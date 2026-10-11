# Fix: The odd-chip rule differs across handlers, and none follows the doc

## Defect

`docs/SidePots.md` §4.3 and §10.5 are the odd-chip rule. §4.3 is the algorithm. §10.5 is that algorithm with a remainder of 1.

```
share = amount / winners.count
remainder = amount % winners.count
Walk winners in seat order, starting with the first winner clockwise from the dealer button.
Give that winner one extra chip and decrement remainder.
Stop when remainder is 0.
```

`remainder` is always smaller than the winner count, so one pass finishes it. The chips of one pot are not saved for the next pot. A single winner gets the whole amount, because `amount % 1` is 0.

What the handlers do instead:

| Live path | What it does with the remainder |
|---|---|
| Hold'em showdown, which also runs Red River | The whole remainder goes to `winners[0]`. |
| Seven Card Stud, Good Bad Ugly, Baseball, Follow the Queen, Kings and Lows endpoint | Same. The comment says "closest to dealer's left". `winners[0]` is the first entry in a `Dictionary` filled by walking `game.GamePlayers` with no `OrderBy`. |
| Generic `AwardAmount`, which is the live showdown for Omaha, Nebraska, South Dakota, Bob Barker, Klondike, Irish Hold'em, Phil's Mom, Crazy Pineapple, Hold the Baseball, Razz, Pair Pressure, Tollbooth, and Screw Your Neighbor | One chip at a time, in that same unordered dictionary order. |
| Five Card Draw | One chip at a time, in `potWinners` order, which is that dictionary order. |
| Kings and Lows `PerformShowdownAsync` | One chip at a time. The winners are staying players ordered by `SeatPosition` from the lowest seat, not from the seat after the button. |
| Twos, Jacks and the Man with the Axe | One chip at a time, but the order is `(DealerPosition + i) % GamePlayers.Count` used as an index into the seat-sorted list. `DealerPosition` is a seat number, not an index. |

`DealerPosition` is a seat number. `CreateGame` sets it to 0. `MoveDealer` sets it to the next occupied `SeatPosition`. `FindFirstActivePlayerAfterSeat` starts at `(seatPosition + 1)` and walks seat numbers. The first player clockwise from the button is the occupied seat immediately after `DealerPosition`, wrapping to the lowest seat.

The hand that distinguishes all of these. Seats 0, 2, and 5. `DealerPosition` is 2. Alice sits 0, Bob sits 2, Carol sits 5. Pot 100. All three tie.

Clockwise from seat 2 the seats are 5, then 0, then 2. Carol is first.

| Player | Doc | Whole remainder to whoever the dictionary yields first | Lowest-seat one-chip walk | Twos/Jacks `% Count` index |
|---|---|---|---|---|
| Carol | 34 | 33, unless she happens to be first | 33 | not first |
| Alice | 33 | 34 if she was inserted first | 34 | index `(2 + 1) % 3 = 0`, so she is first and gets 34 |
| Bob | 33 | 33 | 33 | 33 |

Pot 101, same tie. `101 / 3` is 33, remainder 2. The doc pays Carol 34, Alice 34, Bob 33. The whole-remainder handlers pay 35 to whichever name is `winners[0]` and 33 to the other two.

## Do not change

- Who wins. Strength, wild cards, Razz low, and Bob Barker showcase qualification stay as they are. This only reorders an existing winner set before the extra chips are dealt.
- A pot that one player wins. That player still receives `amount`.
- Bob Barker's split of one pot: `mainHandAmount = (pot.Amount + 1) / 2`, showcase is the rest. Each half is distributed on its own. The odd chip of the pot stays in the main half. The showcase half has its own remainder.
- Twos/Jacks' split of one pot: `sevensPool = amount / 2`, `handPool = amount - sevensPool`. Each half is distributed on its own. No eligible sevens winner still rolls that half into the high half.
- Side-pot amounts and eligibility. Plans 07 through 11 say to keep today's remainder rule. This plan replaces that sentence only. Each pot, layer, or half still uses this distribution on its own amount. Do not add the remainders together and deal them once at the end.
- `MoveDealer`. Read `game.DealerPosition` before it runs. The button for this hand is the seat that dealt it.
- `DealersChoiceDealerPosition`. It is not the button for the odd chip.
- Settlement. It already pays whatever the payout dictionary contains. After this change the odd chip changes which row is larger by 1. Do not change the formula.
- `HandSettlementService`, uncalled bets, and folded-player eligibility.

## One distribution

Add `src/CardGames.Poker.Api/Features/Games/Common/Showdown/OddChip.cs`.

```csharp
internal static class OddChip
{
	internal static List<T> OrderAfterDealer<T>(
		IEnumerable<T> winners,
		Func<T, int> seat,
		int dealerPosition)
	{
		return winners
			.OrderBy(winner => seat(winner) > dealerPosition ? 0 : 1)
			.ThenBy(seat)
			.ToList();
	}

	internal static Dictionary<string, int> Distribute(
		int amount,
		IReadOnlyList<(string Name, int Seat)> winnersInOrder)
	{
		var payouts = new Dictionary<string, int>(StringComparer.Ordinal);
		if (amount <= 0 || winnersInOrder.Count == 0) return payouts;

		var share = amount / winnersInOrder.Count;
		var remainder = amount % winnersInOrder.Count;
		foreach (var winner in winnersInOrder)
		{
			var payout = share;
			if (remainder > 0)
			{
				payout++;
				remainder--;
			}

			payouts[winner.Name] = payouts.GetValueOrDefault(winner.Name) + payout;
		}

		return payouts;
	}
}
```

`OrderAfterDealer` is the clockwise walk. Seats greater than `dealerPosition` come first, ascending. The remaining seats follow, ascending. Seat 2 with winners at 0, 2, and 5 yields 5, 0, 2. A winner sitting on the button is last among the seats that wrap. Empty seats are not in the list, so they are skipped.

`Distribute` implements §4.3. Callers pass winners already ordered. The returned values are that pot's or half's payouts only. Callers add them to the hand's payout dictionary the way they already add a per-pot payout. Use each handler's existing name key. Do not change a dictionary's comparer.

Do not use `(DealerPosition + i) % GamePlayers.Count`. Do not use `+ 1000` as a sort key. Do not give `remainder` to one winner in a lump unless that player is the only winner, in which case `remainder` is 0 and `share` is the pot.

## Call sites

At each site, build `(Name, Seat)` from the winners that site already chose, order them with `game.DealerPosition`, and replace the local share/remainder loop with `Distribute`. Add those amounts into the handler's existing payout map. `ChipStack` increases by that same amount, which the existing code already does from the payout map.

1. `HoldEm/.../PerformShowdownCommandHandler.cs`, the block at the `payoutPerWinner` / `winners[0]` lines. `Seat` is `playerHandEvaluations[name].gamePlayer.SeatPosition`. This is the Red River showdown too.

2. The same block in:
   - `SevenCardStud/.../PerformShowdownCommandHandler.cs`
   - `GoodBadUgly/.../PerformShowdownCommandHandler.cs`
   - `Baseball/.../PerformShowdownCommandHandler.cs`
   - `FollowTheQueen/.../PerformShowdownCommandHandler.cs`
   - `KingsAndLows/.../PerformShowdownCommandHandler.cs`
   - `HoldTheBaseball/.../PerformShowdownCommandHandler.cs`

   A live Hold the Baseball table uses Generic, not this last handler. Change it anyway so the copy does not keep the lump-sum rule.

3. `Generic/.../PerformShowdownCommandHandler.cs`, inside `AwardAmount`. Reorder `winners` with `OrderAfterDealer` before the loop. `Seat` is `eligibleHands[name].GamePlayer.SeatPosition`. `AwardAmount` is called once per pot, and twice for Bob Barker (main half, then showcase half). Each call distributes only the `amount` it was given. `game.DealerPosition` is in scope on the handler; pass it into `AwardAmount`.

4. `FiveCardDraw/.../PerformShowdownCommandHandler.cs`, the `potPayoutPerWinner` loop. `Seat` is `eligibleHands[winner].gamePlayer.SeatPosition`. Once per pot.

5. `KingsAndLowsFlowHandler.PerformShowdownAsync`, the `sharePerWinner` loop. The winners are `GamePlayer`s. Order those players, then pay. This runs before `MoveDealer`.

6. `TwosJacksManWithTheAxe/.../PerformShowdownCommandHandler.cs`. Delete the `orderedPlayerNames` loop that uses `% game.GamePlayers.Count`. For the sevens half, order `sevensWinners` by each player's seat. For the high half, order `highHandWinners` the same way. `Distribute` each half. A name that wins both halves is the sum of the two results, which the handler already does.

If a side-pot plan has already replaced one of these loops with a per-layer award, call `Distribute` on each layer's winners and that layer's amount. Do not put the layer amounts back into one pot first.

Delete the comments that say the first winner is closest to the dealer's left. After this change the order is the code, and those comments described a rule the old code did not run.

## Worked hands

`DealerPosition` is 2. Alice seat 0, Bob seat 2, Carol seat 5. All three tie. Read the button before `MoveDealer`.

### 1. Pot 100, one winner set

`OrderAfterDealer` is Carol, Alice, Bob. `share` 33, `remainder` 1.

| Player | Payout |
|---|---|
| Carol | 34 |
| Alice | 33 |
| Bob | 33 |

Sum 100.

### 2. Pot 101, one winner set

`share` 33, `remainder` 2.

| Player | Payout |
|---|---|
| Carol | 34 |
| Alice | 34 |
| Bob | 33 |

Sum 101. Carol does not receive 35.

### 3. Pot 100, Carol folded

Alice and Bob tie. Clockwise from seat 2, the next winner is Alice (seat 0), then Bob. Alice 50, Bob 50. Remainder 0.

### 4. Pot 50, only Bob

Bob 50. No split.

### 5. Twos/Jacks pot 101, same three seats, all three have a natural pair of sevens and the same high hand

`sevensPool` 50, `handPool` 51. Sevens: `50 / 3` is 16, remainder 2, so Carol 17, Alice 17, Bob 16. High: `51 / 3` is 17, remainder 0, so 17 each. Totals: Carol 34, Alice 34, Bob 33. The `% Count` index is not used.

### 6. Bob Barker pot 101, same three-way tie for both halves

Main half `(101 + 1) / 2` is 51. Showcase half is 50. Main remainder 0, so 17 each. Showcase remainder 2, so Carol 17, Alice 17, Bob 16. Totals: Carol 34, Alice 34, Bob 33. The 51/50 split is unchanged.

### 7. Two side pots

Main 100, the three players tie, Carol 34, Alice 33, Bob 33. Side 50, only Alice and Bob are eligible and they tie, Alice 25, Bob 25. Hand totals: Carol 34, Alice 58, Bob 58. The side pot does not inherit the main pot's leftover.

## Tests

Add `src/Tests/CardGames.Poker.Tests/Api/Features/Games/Common/Showdown/OddChipTests.cs`. No database. Cover hands 1, 2, 3, and 4 by calling `OrderAfterDealer` and `Distribute`. Assert the order Carol, Alice, Bob for dealer seat 2, and Alice then Bob when Carol is absent.

Add one Hold'em showdown test beside the existing Hold'em showdown tests. Seats 0, 2, 5, `DealerPosition` 2, one pot of 100, three equal hands. Assert stacks or the payout map: Carol 34, Alice 33, Bob 33. A second case with pot 101 asserts Carol 34, Alice 34, Bob 33, and that the sum of payouts equals 101.

Add one Twos/Jacks showdown test, or a direct test of the payout method if the handler is too heavy, for hand 5. The assertion is the totals, not the old index order.

Update any existing assertion that pinned the old recipient of a remainder. A test that used a pot divisible by the winner count does not change. A test that paid a remainder to `winners[0]` or to the lowest seat must expect the seat after `DealerPosition`.

Run:

```text
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~OddChipTests|FullyQualifiedName~PerformShowdown"
```

## Done when

- Pot 100 with seats 0, 2, and 5 and the button on 2 pays Carol 34, Alice 33, Bob 33 in every live showdown listed above.
- Pot 101 pays Carol 34, Alice 34, Bob 33, and the payouts sum to 101.
- Bob Barker and Twos/Jacks still split the pot in half first, then deal each half's own remainder in that seat order.
- The `% GamePlayers.Count` remainder walk is gone.
- `DealerPosition` is read before `MoveDealer`.
