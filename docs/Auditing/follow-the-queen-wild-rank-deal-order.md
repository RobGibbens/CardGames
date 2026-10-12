# Fix: Follow the Queen showdown wild rank follows DealOrder, which is not deal order

This plan is the implementation spec for one defect. An LLM should follow it exactly. Do not fix other audit findings in the same change (side pots and uncalled chips, fixed-limit bet size, Tollbooth all-in runout, bring-in action order, ace-low bring-in, odd-chip direction, `BestCardIndexes` from `GetBestHand`).

The sequence already used to label the wild rank on the table is the sequence the payout must use. Do not invent a second ordering.

## Outcome

At Follow the Queen showdown, the face-up list passed to `FollowTheQueenWildCardRules` and to `FollowTheQueenHand` is the order the cards were dealt face up:

- One face-up card per occupied seat, walking seats clockwise from the seat left of the dealer.
- Finish a street before starting the next street. Third-street upcards, then fourth, then fifth, then sixth.
- Seventh-street cards and hole cards are not in the list.
- Face-up cards of folded players stay in the list. A queen they caught still sets the following rank.
- Queens (value 12) are always wild. The rank of the card immediately after the last face-up queen in this list is also wild. A later face-up queen replaces that rank. A queen with no face-up card after it leaves only queens wild.

`FollowTheQueenHand.Strength` is what selects the winner. After this change, that strength uses the same following rank as `TableStateBuilder.ComputeFollowTheQueenWildRanksAsync`.

Only this showdown handler writes the Follow the Queen winner. Pair Pressure, Seven Card Stud, Razz, Baseball, and Good Bad Ugly do not use this list.

## Leave these alone

- `FollowTheQueenWildCardRules.DetermineWildRanks`. The rule is already "the next card in the list you were given." The bug is the list.
- `FollowTheQueenGame` in `src/CardGames.Poker/Games/FollowTheQueen/FollowTheQueenGame.cs`. Its `_faceUpCardsInOrder` appends each board card as that card is dealt, street by street, seats left of the dealer first. That list is the rule. The live table does not call `FollowTheQueenGame.PerformShowdown`.
- Dealers. Do not renumber `GameCard.DealOrder` in `FollowTheQueenFlowHandler.DealCardsAsync` or in `Features/Games/FollowTheQueen/v1/Commands/DealHands/DealHandsCommandHandler.cs`. Third street stores one global counter. Later streets store `existingCardCount + 1` on each player, so every fourth-street card is `DealOrder` 4, every fifth-street card is 5, and every sixth-street card is 6. Those values collide with third-street upcards. Other readers use `DealOrder` for per-player ordering. Rewriting them is a different change.
- Per-player hole, open, and seventh card sets inside the showdown loop. `FollowTheQueenHand` uses those sets as a bag of cards. Strength does not depend on their order. The wild rank depends only on the shared face-up list.
- `BestCardIndexes`. They still come from `StudHand.GetBestHand()`, which ignores wilds. Do not switch them to `BestHandSourceCards` in this change.
- `WildCardIndexes`. They index the player's cards after that method's own `OrderBy(DealOrder)`. Leave that indexing as it is. The rank set those indexes test will change because `wildRanks` comes from the corrected list. Do not rebuild the response `Cards` list.
- Payout math. Equal contributions in the tests below make the winner change visible with the current single-pot payout. Do not layer side pots here.
- Pair Pressure. `TableStateBuilder.GetOrderedFaceUpCardsAsync` is also called for Pair Pressure display, and generic showdown has its own face-up sort. Moving the existing table-state body into a shared loader must keep Pair Pressure's displayed order the same. Do not point generic showdown at the new loader.

## Why the current sort pays the wrong player

`PerformShowdownCommandHandler` loads visible cards and sorts them with:

```csharp
.OrderBy(c => c.DealOrder)
.ThenBy(c => c.GamePlayer!.SeatPosition)
```

That query is in `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`, in the block commented "Load face-up cards in deal order." The same list is passed to `DetermineWildRanks` and to `new FollowTheQueenHand(...)`.

`DealOrder` is not that deal order.

Third street is dealt by `FollowTheQueenFlowHandler.DealCardsAsync`. One counter starts at 1. Each player receives two hole cards and one upcard before the next player. With dealer seat 2 and seats 0, 1, 2, the deal order is seat 0, seat 1, seat 2. The upcards are stored as `DealOrder` 3, 6, and 9.

Fourth, fifth, and sixth streets are dealt by the Follow the Queen `DealHandsCommandHandler`. For each player, `playerDealOrder` starts at `existingCardCount + 1`. After third street every player has 3 cards, so every fourth-street card is `DealOrder` 4. Fifth street is 5. Sixth street is 6. Seat 1's third-street upcard is also `DealOrder` 6.

Sorting all visible cards by `DealOrder`, then by seat, puts every fourth-street card (4) between seat 0's third-street upcard (3) and seat 1's third-street upcard (6). The card "after" a third-street queen is then that same player's fourth-street card.

The table does not use that sort. `TableStateBuilder.GetOrderedFaceUpCardsAsync` (`src/CardGames.Poker.Api/Services/TableStateBuilder.cs`) loads the same visible, non-discarded rows, groups them by seat, orders each seat with `StudOrderHelper.OrderPlayerCards`, then interleaves seats with `StudOrderHelper.OrderFaceUpCardsInGlobalDealOrder`. `GetRotationOrder` is `seat > dealer ? seat : seat + 1000`, which is the same seat walk `DealHands` uses. Within a seat, third-street board cards sort before fourth street because `GetPlayerCardOrderKey` uses `DealtAtPhase` before `DealOrder`. The next face-up card after seat 0's queen is seat 1's third-street upcard.

`ComputeFollowTheQueenWildRanksAsync` walks that list. The screen can show nines wild while the payout evaluates deuces wild.

## Shared loader

Add one API helper, for example `src/CardGames.Poker.Api/Features/Games/Common/StudFaceUpSequence.cs`. It owns the query and the call into `StudOrderHelper`. Do not put EF types in the `CardGames.Poker` project. Do not change `StudOrderHelper`.

`LoadAsync(CardsDbContext context, Guid gameId, int handNumber, int dealerPosition, CancellationToken cancellationToken)` returns `List<Card>`:

1. Load `GameCards` for that game and hand where `IsVisible` is true and `IsDiscarded` is false. Project `Symbol`, `Suit`, `DealtAtPhase`, `DealOrder`, `Location`, and `GamePlayer.SeatPosition`. A card with no player uses seat `-1`, matching the table builder. Do not `OrderBy(DealOrder)` in the query. Ordering happens in memory after the rows are loaded.
2. Group by seat.
3. Order each group with `StudOrderHelper.OrderPlayerCards`, using `DealtAtPhase`, `Location == CardLocation.Hole`, and `DealOrder`.
4. Pass those groups to `StudOrderHelper.OrderFaceUpCardsInGlobalDealOrder` with `dealerPosition`, the seat selector, the ordered-card selector, and `_ => true`.
5. Map each row to `new Card((Suit)suit, (Symbol)symbol)`.

This is the current body of `TableStateBuilder.GetOrderedFaceUpCardsAsync`, moved, not rewritten. Replace that private method's body with a call to `LoadAsync(context, game.Id, game.CurrentHandNumber, game.DealerPosition, cancellationToken)`. `ComputeFollowTheQueenWildRanksAsync` and `ComputePairPressureWildRanksAsync` keep calling `GetOrderedFaceUpCardsAsync`. Their results must not change for a hand whose `DealOrder` values already increase within each seat and street.

In the Follow the Queen showdown handler, delete the `OrderBy(DealOrder).ThenBy(SeatPosition)` query. Call `StudFaceUpSequence.LoadAsync` with `command.GameId`, `game.CurrentHandNumber`, and `game.DealerPosition`. Store one list. Pass that list to `DetermineWildRanks` and to every `FollowTheQueenHand` constructed in the loop. Do not build a second list for the hand.

Do not set `IsVisible = true` on hole cards or seventh-street cards before this load. The showdown response reveals those cards in the DTO. They are not part of the face-up sequence. The load runs before payouts are written; keep it there.

Do not filter the list down to `playersInHand`. A folded seat's visible board card is still a dealt face-up card.

## Worked hand

Dealer is Carol, seat 2. Occupied seats are 0, 1, and 2, so the rotation is Alice (0), Bob (1), Carol (2). Each player contributed 100. The pot row is 300. Before showdown each stack is 900 (they started at 1000). Phase is showdown. No payout has been recorded.

Persist these `GameCard` rows. `IsVisible` is true only on `Location == Board`. Hole and seventh rows are `IsVisible` false and `Location == Hole`. None are discarded.

Third street, from the global counter in `DealCardsAsync`:

| Player | Seat | Cards | DealOrder | Phase | Visible |
|---|---|---|---|---|---|
| Alice | 0 | Ah, Ac | 1, 2 | ThirdStreet | no |
| Alice | 0 | Qh | 3 | ThirdStreet | yes |
| Bob | 1 | Kh, Kd | 4, 5 | ThirdStreet | no |
| Bob | 1 | 9c | 6 | ThirdStreet | yes |
| Carol | 2 | 6s, 6c | 7, 8 | ThirdStreet | no |
| Carol | 2 | 4d | 9 | ThirdStreet | yes |

Later streets, from `existingCardCount + 1` in `DealHandsCommandHandler`. Every player on a street shares one `DealOrder`.

| Street | DealOrder | Alice | Bob | Carol |
|---|---|---|---|---|
| FourthStreet | 4 | 2s | 3h | 5c |
| FifthStreet | 5 | 7d | 9d | 7c |
| SixthStreet | 6 | 8d | 4h | 8c |
| SeventhStreet | 7 | 3d | 5h | 9h |

Seventh street is not visible.

True face-up order, which `StudOrderHelper` produces for dealer seat 2:

`Qh, 9c, 4d, 2s, 3h, 5c, 7d, 9d, 7c, 8d, 4h, 8c`

The only face-up queen is `Qh`. The next card is `9c`. Wild ranks are queens and nines.

Current showdown order starts `Qh` (`DealOrder` 3), then `2s` (`DealOrder` 4, seat 0). Wild ranks become queens and deuces. Bob's `9c` is `DealOrder` 6, so it sorts with the sixth-street cards, not as the card after the queen.

Hands with queens and nines wild:

- Alice holds Ah, Ac, Qh, 2s, 7d, 8d, 3d. Only Qh is wild. Best hand is trips aces.
- Bob holds Kh, Kd, 9c, 3h, 9d, 4h, 5h. 9c and 9d are wild with Kh Kd. Best hand is quads kings.
- Carol holds 6s, 6c, 4d, 5c, 7c, 8c, 9h. 9h is wild. Four clubs plus that wild make a flush. A flush loses to quads.

Bob wins the 300. Stacks after showdown: Alice 900, Bob 1200, Carol 900. Payout key is Bob for 300. Alice and Carol are not winners.

Hands with queens and deuces wild, which is what the current sort pays:

- Alice's Qh and 2s are wild with Ah Ac. Best hand is quads aces.
- Bob has no queen and no deuce. Best hand is two pair, kings and nines.
- Carol's straight 4-5-6-7-8 loses to quads aces.

That payout gives the 300 to Alice. The test must fail on the current handler and pass after the loader is used.

Carol's flush in the corrected evaluation matters. Do not assert that Carol still has only a straight once nines are wild.

## Second hand: the queen is last

Dealer seat 2. Same three seats and the same `DealOrder` pattern. Change only these visible cards:

- Carol's third-street upcard is 9c at `DealOrder` 9.
- Carol's sixth-street card is Qc at `DealOrder` 6.
- Alice holds Ah, Ac, and a hole Qd, plus open cards 2s, 3h, 4d, 5c and seventh 7h.
- Bob holds Kh, Kd, hole 9h and 9d, plus open cards that are not queens, nines, or aces (8c, 8d, 7c, 6c) and seventh 5h.

True order ends with Carol's Qc. Nothing is dealt face up after it. Wild ranks are queens only.

- Alice's hole Qd is wild with Ah Ac. Best hand is trips aces.
- Bob's nines are natural. Best hand is two pair.

Current sort places Qc at `DealOrder` 6 and then Carol's 9c at `DealOrder` 9, so nines become wild. Bob's two hole nines plus Kh Kd become quads kings, and Bob is paid.

After the fix, Alice is paid the 300. Bob is not.

This case is why a larger `DealOrder` must not be treated as "later in the hand." Carol's third-street upcard has a higher `DealOrder` than her sixth-street queen.

## Tests

Add `PerformShowdownCommandHandlerTests` under `src/Tests/CardGames.Poker.Tests/Api/Features/Games/FollowTheQueen/v1/Commands/PerformShowdown/`. Follow the in-memory `CardsDbContext` setup in `src/Tests/CardGames.Poker.Tests/Api/Features/Games/SevenCardStud/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`: register the API assembly with MediatR, seed a game whose type code is `FOLLOWTHEQUEEN`, phase showdown, dealer seat 2, three players, one pot of 300, and the `GameCard` rows above. Call the Follow the Queen `PerformShowdownCommand`.

Assert for the first hand:

- The success result's only payout is Bob for 300.
- Bob's `HandType` is `Quads`.
- Alice's `HandType` is `Trips`.
- Carol's `HandType` is `Flush`.
- Stacks are Alice 900, Bob 1200, Carol 900.
- Calling the handler a second time does not add another 300. The existing `isAlreadyAwarded` path must keep that behavior; do not rewrite it.

Assert for the second hand:

- The only payout is Alice for 300.
- Alice's `HandType` is `Trips`.
- Bob's `HandType` is `TwoPair`.

Add one focused test around `StudFaceUpSequence.LoadAsync` that seeds only the visible rows from the first hand and asserts the 12-card order `Qh, 9c, 4d, 2s, 3h, 5c, 7d, 9d, 7c, 8d, 4h, 8c`. That locks the loader without going through payout.

Do not change `FollowTheQueenWildCardRulesTests` or `FollowTheQueenHandTests`. They pass an already ordered list. Do not change `StudOrderHelperTests`.

Run the new tests and the existing `TableStateBuilder` Follow the Queen tests in `src/Tests/CardGames.IntegrationTests/Services/TableStateBuilderTests.cs`. The move of `GetOrderedFaceUpCardsAsync` must leave those green without editing their assertions.

## Done when

- Showdown and `TableStateBuilder.GetOrderedFaceUpCardsAsync` both call `StudFaceUpSequence.LoadAsync`.
- The showdown handler has no `OrderBy(c => c.DealOrder)` on the face-up query.
- `DealOrder` assignment in both Follow the Queen dealers is unchanged.
- `FollowTheQueenWildCardRules` is unchanged.
- The first worked hand pays Bob 300 and records quads for Bob, trips for Alice, and a flush for Carol.
- The second worked hand pays Alice 300 because the sixth-street queen has no face-up card after it.
- Pair Pressure display still uses the same `StudOrderHelper` walk it used before the move.
