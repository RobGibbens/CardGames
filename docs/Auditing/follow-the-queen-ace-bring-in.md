# Fix: Follow the Queen bring-in treats an ace as the lowest door card

This plan is the implementation spec for one defect. An LLM should follow it exactly. Do not fix other audit findings in the same change (side pots, Follow the Queen wild-card order, fixed-limit bet size, Tollbooth all-in runout, bring-in action order, odd chips).

An ace is the highest door card. The lowest door card posts the bring-in. That is already how `FollowTheQueenGame.CompareCardsForBringIn` works, because `Card.Value` for an ace is 14. It is already how every other stud flow works, and how Follow the Queen `DealHandsCommandHandler.GetCardValue` works. The continuous-play deal does not.

## Outcome

`FollowTheQueenFlowHandler.FindBringInPlayer` selects the same player as Seven Card Stud, Baseball, Pair Pressure, Tollbooth, and Follow the Queen `DealHandsCommandHandler`:

- Rank order is deuce through ace: 2, 3, 4, 5, 6, 7, 8, 9, 10, J, Q, K, A. An ace is 14. A deuce is 2. A king is 13.
- The lower rank posts. An ace does not post when any non-ace door card is in play.
- A tie is broken by suit: clubs, then diamonds, then hearts, then spades. Clubs is the lowest suit. This is `GetSuitRank` in `SevenCardStudFlowHandler` and in the Follow the Queen deal handler. It is not `CardSuit`'s numeric order.
- The selected player is stored the same way this flow stores the bring-in today: `BringInPlayerIndex`, `CurrentPlayerIndex`, and `CurrentActorIndex` are that seat, and `PostBringInForThirdStreetAsync` posts `Game.BringIn` from that stack. Do not move the first voluntary action off that seat. That is a separate plan.

## Leave these alone

- `FollowTheQueen` `DealHandsCommandHandler.GetCardValue`. `CardSymbol.Ace` is already 14, and `GetSuitRank` already ranks clubs lowest. Do not add a second copy of the ranking, and do not change this method to match the old flow.
- `CardGames.Poker/Games/FollowTheQueen/FollowTheQueenGame.cs` `CompareCardsForBringIn`. It compares `Card.Value`. `Symbol.Ace` is 14.
- Seven Card Stud, Razz, Baseball, Pair Pressure, and Tollbooth bring-in comparisons. They already use ace 14.
- `ScrewYourNeighborFlowHandler` and `BobBarkerFlowHandler`. Those games rank an ace as 1 for their own rules.
- Who acts after the bring-in is posted. The flow may keep the poster as the current actor until the bring-in action-order plan changes that.
- Wild-card evaluation. A queen is still wild. An ace being high for the bring-in does not change hand strength.

## Why an ace posts

Continuous play deals third street through `FollowTheQueenFlowHandler.DealCardsAsync`. That method deals two hole cards and one door card per player from the persisted deck, in seat order starting left of the dealer, then calls `FindBringInPlayer`.

`FindBringInPlayer` orders door cards with `GetRankValue`. That switch maps `CardSymbol.Ace` to 1 and `CardSymbol.Deuce` to 2. The comment above the call says "Ace is low for bring-in." The lowest value posts.

`CardSuit` is not suit rank. Its values are `Hearts = 0`, `Diamonds = 1`, `Spades = 2`, `Clubs = 3`. The same method then orders ties with `.ThenBy(p => p.UpCard.Suit)`. Two aces therefore bring in hearts before clubs. The comment above that line says clubs is lowest. The deal handler's `GetSuitRank` matches the comment: clubs 0, diamonds 1, hearts 2, spades 3.

`DealHandsCommandHandler` is a different entry. A client that deals through that command already posts the deuce against an ace. A hand started by the flow handler posts the ace. Both paths are live. This fix changes only the flow handler, so the two paths agree.

## The comparison

Replace `GetRankValue` and the `OrderBy`/`ThenBy` in `FindBringInPlayer` with the comparison already used by `SevenCardStudFlowHandler.CompareCardsForBringIn`:

- Compare `GetCardValue`. Use the same switch as `SevenCardStudFlowHandler.GetCardValue`, including `CardSymbol.Ace => 14`. Delete the ace-is-low comment.
- If the ranks differ, the smaller rank brings in.
- If the ranks match, compare `GetSuitRank`: clubs 0, diamonds 1, hearts 2, spades 3. Do not pass `CardSuit` to `OrderBy` or `ThenBy`. Hearts would sort first.
- The first card that is strictly lower than the current lowest card wins. Walk the door cards the way `SevenCardStudFlowHandler.FindBringInPlayer` walks them. Equal cards do not replace the current lowest, so the earlier player in the list keeps a perfect tie. The flow's door-card list is built in deal order, left of the dealer first.

No other method in this file uses `GetRankValue`. Queens stay wild. Kings stay 13.

## Worked hand

Alice is seat 0. Bob is seat 1. The dealer is Bob, so the deal starts with Alice. `Game.BringIn` is 5. Neither player has posted an ante. Both stacks are 1000. No other chips are in the pot.

Persist a deck, ordered by `DealOrder`, before calling `DealCardsAsync`. The flow uses that deck and does not shuffle when it already exists.

| DealOrder | Card | Goes to |
|---|---|---|
| 0 | Kd | Alice hole |
| 1 | Qc | Alice hole |
| 2 | Ah | Alice door |
| 3 | 7s | Bob hole |
| 4 | 9d | Bob hole |
| 5 | 2c | Bob door |

`GetRankValue` today maps Ah to 1 and 2c to 2. Alice posts 5. Alice's stack is 995, her `CurrentBet` is 5, and `BringInPlayerIndex` is 0. Bob's stack stays 1000 and his `CurrentBet` stays 0.

After the fix, 2 is lower than 14. Bob posts 5. Bob's stack is 995, his `CurrentBet` is 5, and `BringInPlayerIndex` is 1. Alice's stack stays 1000 and her `CurrentBet` stays 0. The pot is 5.

Two more decks, same seats and the same hole cards, prove the neighbors of this rule:

- Alice's door is Kh and Bob's door is Ah. The king is lower. Alice posts. Today Bob posts, because the ace is 1 and the king is 13.
- Alice's door is Ah and Bob's door is Ac. Clubs is the lower suit. Bob posts. Today Alice posts, because `CardSuit.Hearts` is 0 and `CardSuit.Clubs` is 3.

A deuce still posts against a three. That case is already true and must stay true.

## Tests

Add a flow test beside `src/Tests/CardGames.IntegrationTests/GameFlow/StreetFlowDeckIdempotencyTests.cs`, which already constructs `FollowTheQueenFlowHandler` and calls `DealCardsAsync`. Seed the persisted deck from the worked hand, with `BringIn` 5 and stacks 1000. Assert Bob is the only player with `CurrentBet` 5, his stack is 995, and `BringInPlayerIndex` is seat 1.

Add the king-versus-ace deck and the two-ace deck as two more cases on that test. The king posts against the ace. The ace of clubs posts against the ace of hearts.

Do not change `FollowTheQueenDealHandsCommandHandlerTests`. Its third-street case uses a jack against a king, and the deal handler already ranks an ace as 14. Run it. It stays green without an assertion edit.

Run `FollowTheQueenGameTests.GetBringInPlayer_ReturnsPlayerWithLowestUpcard`. That test uses the domain game. Do not change it.

## Done when

- `FollowTheQueenFlowHandler` has no `CardSymbol.Ace => 1` and no comment that an ace is low for the bring-in.
- Door-card ties use clubs, diamonds, hearts, spades. They do not use `CardSuit`'s declared order.
- On the worked hand Bob posts the 5 and Alice does not.
- A king posts against an ace. An ace of clubs posts against an ace of hearts.
- `DealHandsCommandHandler.GetCardValue` still maps an ace to 14.
- Screw Your Neighbor and Bob Barker still map an ace to 1.
