# Fix: split-pot odd chips use a different seat than Razz, Pair Pressure, and Tollbooth

This plan is the implementation spec for one defect. An LLM should follow it exactly. Do not fix other audit findings in the same change (side pots, Follow the Queen wild-card order, fixed-limit bet size, Tollbooth all-in runout, bring-in action order, ace-low bring-in).

Razz, Pair Pressure, and Tollbooth show down through generic `AwardPotsAsync`. That method already hands leftover chips to the tied players in seat order, one chip at a time, starting with the first occupied winner seat left of the dealer. The dedicated stud showdowns do not. They add the whole remainder to `winners[0]`.

## Outcome

When a contested showdown splits one pot across two or more winners, each winner receives `pot / winnerCount`. Each leftover chip goes to one winner. The first leftover chip goes to the winner whose seat is first in this order:

1. Seats greater than `Game.DealerPosition`, lowest seat first.
2. Seats less than or equal to `Game.DealerPosition`, lowest seat first.

The next leftover chip goes to the next winner in that same order. A remainder of 2 gives one extra chip to each of the first two winners. It does not give both chips to one player.

This is the sort already inside generic `AwardAmount`:

```csharp
.OrderBy(seat => seat > game.DealerPosition ? 0 : 1)
.ThenBy(seat => seat)
```

A single winner still receives the whole pot. A win by fold still gives the whole pot to the last player. Neither path has a remainder to place.

## Leave these alone

- Which hand wins. Seven Card Stud stays on `StudHand`. Baseball stays on `BaseballHand`. Follow the Queen stays on `FollowTheQueenHand`. Good Bad Ugly stays on `GoodBadUglyHand`, and a player marked `UGLY_ELIMINATED` stays out of the winner list when any non-eliminated player remains. Odd chips are distributed only across the winners that handler already selected.
- Side pots. These four handlers still add every current-hand pot into one `totalPot` and split that sum. Do not build contribution layers in this change. The remainder is `totalPot % winners.Count` of that same sum.
- Generic `AwardPotsAsync` results for Razz, Pair Pressure, and Tollbooth. Extracting the sort they already use must not change who they pay. Their handlers keep forwarding to generic showdown.
- The Razz branch in the Seven Card Stud showdown. It returns to generic showdown before the stud payout. Do not edit that branch.
- Hold'em `AwardPotAsync`. It already uses this same seat sort and the same one-chip loop. Do not edit it.
- The win-reason string. It may keep reading a winner's hand type from the unsorted winner list. Only the chip amounts change.
- An already awarded pot. The second showdown call reconstructs payouts from `WinnerPayouts`. It must not move the odd chip again.

## Why the seats differ

`playersInHand` is `game.GamePlayers` filtered to players still in the hand, with no seat sort. The handler inserts those players into a dictionary in that order. `winners` is the tied names taken from the dictionary. `winners[0]` is whoever was inserted first.

The comment above the remainder says the extra chips go to the player closest to the dealer's left. The code does not look at `DealerPosition`:

```csharp
var payoutPerWinner = totalPot / winners.Count;
var remainder = totalPot % winners.Count;
foreach (var winner in winners) payouts[winner] = payoutPerWinner;
if (remainder > 0 && winners.Count > 0) payouts[winners[0]] += remainder;
```

That block is in:

- `src/CardGames.Poker.Api/Features/Games/SevenCardStud/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/Baseball/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/GoodBadUgly/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`

Generic showdown, which Razz, Pair Pressure, and Tollbooth call, orders the tied names before giving out chips. With dealer seat 0, Bob in seat 1 sorts ahead of Alice in seat 3. Bob receives the leftover chip. If Alice's `GamePlayer` row was loaded first, the stud handler pays Alice.

A remainder larger than 1 is also wrong. Generic gives one chip to each of the first `remainder` seats. The stud handler adds the entire remainder to one name.

## One sort

Add `src/CardGames.Poker.Api/Features/Games/Common/Showdown/OddChipOrder.cs`.

`OrderClockwiseFromDealer<T>(winners, dealerSeat, seatSelector)` returns a new list. It does not mutate the input. The sort is:

```csharp
winners
    .OrderBy(winner => seatSelector(winner) > dealerSeat ? 0 : 1)
    .ThenBy(winner => seatSelector(winner))
    .ToList();
```

`Distribute(amount, orderedWinners)` returns one integer per winner, in that same order. `share` is `amount / orderedWinners.Count`. `remainder` is `amount % orderedWinners.Count`. Walk the ordered winners once. Each receives `share`, and while `remainder` is greater than zero that winner also receives one and `remainder` decreases by one. An empty winner list returns an empty result. Do not give a winner the whole remainder in one assignment.

Replace the `OrderBy` / `ThenBy` and the one-chip loop inside generic `AwardAmount` with these two methods. The names, the amounts, and the pot breakdown fields stay as they are. Razz, Pair Pressure, and Tollbooth then call the helper by staying on generic showdown.

In each of the four dedicated handlers, replace the payout loop and the `winners[0] += remainder` line. Order the existing winner names with `OrderClockwiseFromDealer`, using each winner's `GamePlayer.SeatPosition` and `game.DealerPosition`. Distribute `totalPot`. Write those amounts into `payouts`. The stack update that follows already adds `payouts` to `ChipStack`. Do not add the remainder a second time.

## Worked hand

Dealer seat is 0. Alice is seat 3. Bob is seat 1. Carol has folded and is not a winner. One current-hand pot contains 101. Alice and Bob tie. Both stacks are 100 before the award. The pot is not already awarded.

Clockwise from the dealer, seat 1 comes before seat 3. Bob receives 51. Alice receives 50. After the award Bob's stack is 151 and Alice's stack is 150. The stored `WinnerPayouts` amounts are those same numbers.

The current stud handler pays 51 to whichever tied player was inserted first. When Alice's row is first, Alice finishes at 151 and Bob at 150.

A second distribution, with no showdown required: amount 5, winners in seats 1, 2, and 3, dealer seat 0. The ordered seats are 1, 2, 3. The shares are 2, 2, and 1. The current `winners[0]` assignment would pay 3, 1, and 1 if seat 1 happened to be first, or 3 to a different seat if another row was first.

Dealer seat 2, with the same Alice and Bob tying for 101: seat 3 is the only winner seat greater than 2, so Alice is first and receives 51. Bob receives 50. This is the same rule, not a special case. A test that only uses dealer seat 0 does not lock it.

## Tests

Unit-test `OddChipOrder`:

- Dealer 0, seats 3 then 1 as input: ordered seats are 1, then 3. Distributing 101 yields 51 then 50.
- Dealer 2, seats 1 and 3: ordered seats are 3, then 1. Distributing 101 yields 51 for seat 3 and 50 for seat 1.
- Dealer 0, seats 1, 2, and 3: distributing 5 yields 2, 2, 1.
- One winner, amount 101: that winner receives 101.
- No winners: an empty result.

Extend the Seven Card Stud showdown handler test so Alice in seat 3 and Bob in seat 1 tie, the dealer is seat 0, the pot is 101, and both stacks start at 100. The success payouts are Bob 51 and Alice 50. The stacks are Bob 151 and Alice 150. Build Alice's `GamePlayer` before Bob's so the unsorted winner list still starts with Alice. The test fails on the current handler and passes after the helper is used.

Add the dealer-seat-2 case to that same handler: Alice receives 51 and Bob receives 50.

Add one tied split of 101, dealer seat 0, seats 1 and 3, to the Baseball, Follow the Queen, and Good Bad Ugly showdown tests. Each must pay seat 1 the 51. Good Bad Ugly's tie is between two players who are allowed to win. Do not mark either `UGLY_ELIMINATED`.

Run the existing Razz showdown test and the Hold'em showdown tests. Their assertions stay as they are.

## Done when

- Generic `AwardAmount` and the four dedicated showdowns call `OddChipOrder`.
- None of the four handlers still executes `payouts[winners[0]] += remainder`.
- The worked hand pays Bob 51 and Alice 50 when the dealer is seat 0, and Alice 51 and Bob 50 when the dealer is seat 2.
- A remainder of 2 is two separate chips to the first two seats, not one payment of 2.
- Razz, Pair Pressure, Tollbooth, and Hold'em payouts are unchanged.
