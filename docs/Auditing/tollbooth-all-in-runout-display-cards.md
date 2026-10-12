# Fix: Tollbooth all-in runout never uses the display cards

This plan is the implementation spec for one defect. An LLM should follow it exactly. Do not fix other audit findings in the same change (side pots, Follow the Queen wild-card order, fixed-limit bet size, bring-in action order, ace-low bring-in, odd chips).

A Tollbooth display card is not a community card. `TollboothRules` says the two face-up cards are visible to everyone and are not part of any player's hand until a player takes one. Do not add `TOLLBOOTH` to `UsesSharedCommunityCards`.

## Outcome

When fewer than two non-folded players can bet, every street that has not yet been chosen is filled by the free Tollbooth choice: the furthest display card. The top deck card is used only to replace a display card that was taken, or when no display card remains.

After the runout:

- Each non-folded player has one new card per remaining street, through seventh street.
- Fourth, fifth, and sixth street cards are `Location == Board`, `IsVisible` true, `DealtAtPhase` that street.
- The seventh-street card is `Location == Hole`, `IsVisible` false, `DealtAtPhase` SeventhStreet.
- Folded players receive nothing.
- No player is charged. Furthest costs 0. `ChipStack`, `TotalContributedThisHand`, and the pot do not change inside the runout.
- Two community cards are still on the table when the deck had cards to replenish. They stay `Location == Community`, `GamePlayerId` null, and they are not in anyone's showdown hand.
- The hand then goes to showdown. The generic showdown evaluates each player's own hole and board cards, which now include the cards taken from the display.

## Leave these alone

- A Tollbooth offer while at least two players can still bet. Those players still choose furthest (free), nearest (1× ante), or deck (2× ante) through `ChooseCardCommandHandler`. A paid choice still takes the card they selected and still replenishes only when the choice came from the display.
- `DealRemainingStreetsAsync` for every game that is not Tollbooth. Seven Card Stud, Razz, and Pair Pressure keep taking `Location == Deck` cards in seat order.
- Showdown ranking. Do not teach the evaluator to append leftover community cards. Once a display card has been assigned to a player, the existing stud hand already sees it.
- The bring-in, the fixed-limit bet size, and side pots.

## Why the live runout skips the display

`TollboothFlowHandler.DealCardsAsync` places two cards at `Location == Community` before third-street betting. Later streets are not dealt. `AdvanceToNextPhase` in the stud betting handler turns Fourth through Seventh into `TollboothOffer` so each player can choose.

When `AreAllPlayersAllIn` is true, that same handler resolves `TollboothOffer` back to a street with `ResolveTollboothOfferToStreet` and calls `DealRemainingStreetsAsync`. That method loads only `Location == Deck` cards, ordered by `DealOrder`, and gives each non-folded player the next deck card. The community cards stay on the table.

The same deck loop is copied at the end of `ChooseCardCommandHandler`, in the block that runs when the offer completes and `playersWhoCanBet.Count < 2` and `nextStreet` is not SeventhStreet. The street just chosen uses the player's selection. Every later street uses the deck and ignores the display.

`FindFirstActivePlayerAfterDealer` skips all-in seats, so an all-in table's `CurrentDrawPlayerIndex` becomes -1. `ChooseCard` then rejects the action as not that player's turn. The auto-action, which would have sent `TollboothChoice.Furthest`, never runs. The runout must not depend on that index.

Showdown is `Features/Games/Tollbooth/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`, which forwards to generic showdown. Generic showdown does not include Tollbooth community cards. A display card that was never assigned cannot win the pot.

## One assignment, used by both runouts

Add a helper in the Tollbooth feature folder, for example `src/CardGames.Poker.Api/Features/Games/Tollbooth/TollboothRunout.cs`. Both runouts call it. Do not implement the runout by sending `ChooseCardCommand` in a loop. That command charges a paid choice, rejects a table whose draw index is -1, and its completion path still contains the deck loop.

`DealStreet(game, playersStillInHand, street, now)` assigns one card to each non-folded player for that street.

Player order, matching the offer rather than raw seat order:

1. The first recipient is the seat immediately left of `game.DealerPosition` who has not folded. All-in players are included. This is `FindFirstActivePlayerAfterDealer` with the `!IsAllIn` check removed. Do not change `FindFirstActivePlayerAfterDealer` itself. Betting still must not ask an all-in player to act.
2. Each later recipient is the lowest `SeatPosition` among players still in the hand who have not yet received a card on this street. That is the order `ChooseCardCommandHandler` uses when it picks the next player with `OrderBy(gp => gp.SeatPosition)` after the first actor has already chosen. Do not replace that with a circular walk in this change.

For each recipient, take the card with these rules, copied from the furthest branch of `ChooseCardCommandHandler`:

- Among cards with `Location == Community`, `GamePlayerId == null`, and not discarded, the furthest card is the one with the highest `DealOrder` (`communityCards[^1]` after `OrderBy(DealOrder)`).
- Give that card to the player.
- Replenish before the next player. The replacement is the deck card with the lowest `DealOrder` whose `Location` is still `Deck`. Set it to `Community`, `GamePlayerId` null, `IsVisible` true, `DealtAt` now, `DealtAtPhase` TollboothOffer, and `DealOrder` to one more than the highest `DealOrder` still on the display. The replacement is then the furthest card for the next player.
- Select the deck card at the moment of replenishment. Do not use a list captured before earlier replacements moved cards off the deck. A snapshot still contains cards whose `Location` has already changed, and the next player would receive a card that is no longer in the deck.
- If the display is empty and a deck card remains, give that top deck card and do not replenish. If neither remains, stop the street and leave the later players without a new card. Log the same warning `DealRemainingStreetsAsync` already logs for an empty deck.

Placement of the taken card:

- `GamePlayerId` is the recipient.
- Seventh street: `Location` Hole, `IsVisible` false.
- Every earlier street: `Location` Board, `IsVisible` true.
- `DealtAtPhase` is the street being dealt, not TollboothOffer and not the phase the card had while it was on display.
- `DealtAt` is now.
- `DealOrder` is the count of that player's cards on this hand that are not discarded and not still in the deck, plus one. Count the card just assigned so the next street for that player gets the next number.

Do not change `ChipStack`, `IsAllIn`, `TotalContributedThisHand`, or any pot inside `DealStreet`.

## Call sites

`DealRemainingStreetsAsync` in `src/CardGames.Poker.Api/Features/Games/SevenCardStud/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`:

- Keep the street list and `startIndex`. `ResolveTollboothOfferToStreet` stays. An all-in on sixth street resolves to SeventhStreet and deals only seventh. An all-in on third street resolves to FourthStreet and deals fourth through seventh. A completed seventh-street betting round already advanced to showdown and must not deal an eighth card.
- When `Game.CurrentHandGameTypeCode`, or else `Game.GameType.Code`, is `TOLLBOOTH`, replace the per-player deck assignment for that street with `TollboothRunout.DealStreet`. Still write `allInRunout`, `runoutStreets`, `runoutHandNumber`, and `runoutTimestamp` into `GameSettings`, and still set the phase to showdown.
- For every other game type, the existing deck assignment stays, including its `OrderBy(SeatPosition)` player order.

`ChooseCardCommandHandler`, in the block that deals streets after `nextStreet` when the offer completes all-in: delete the deck loop. For each of those later streets, call `TollboothRunout.DealStreet`. The street the player just chose is already assigned. Do not assign it again. Keep the `allInRunout` settings and the transition to showdown.

## Worked hand

Dealer is Bob, seat 1. Alice is seat 0. With two seats, the first recipient left of the dealer is Alice, then Bob.

Both started with 200 and have 100 left. Each has contributed 100. The pot is 200. Both are all-in. The phase is sixth-street betting, and the action that just completed the round leaves fewer than two players who can bet. The runout deals seventh street only.

Alice's six cards are 9h, 8h, 7h, 6h, 2d, 3c. Bob's six cards are Ac, Ad, Kd, Qd, Jd, Td. Neither has a seventh card yet.

Display, in increasing `DealOrder`: 2c, then Ah. Ah is furthest. Deck, in increasing `DealOrder`: 3d, then 7s.

The runout gives Alice the furthest card, Ah, as a face-down seventh-street hole card. It replenishes from 3d, which becomes the new furthest. It gives Bob 3d as his face-down seventh-street hole card. 2c is still community and has no player. 7s is still in the deck.

Alice's hand is a heart flush. Bob's hand is a pair of aces. The flush wins. Generic showdown pays Alice the 200. Stacks are Alice 300, Bob 100.

The current deck runout gives Alice 3d and Bob 7s. Alice has no flush. Bob's aces win. Stacks are Alice 100, Bob 300. Ah and 2c are still community and are not in either hand.

2c must not become a second card for either player, and it must not be added to the showdown hand as a shared community card.

## Tests

Update `ChooseCard_AllPlayersAllIn_PreSeventhStreet_DealsRemainingAndTransitionsToShowdown` in `src/Tests/CardGames.IntegrationTests/Games/Tollbooth/TollboothChooseCardTests.cs`. It currently chooses the deck, then the handler deals fifth, sixth, and seventh from the deck. Keep the assertions that the phase is showdown, each player has 7 cards, the seventh card is a face-down hole card, and no betting round is left open. Add an assertion that the first fifth-street card belongs to the first runout recipient and is the community card that had the higher `DealOrder` before that street, not the deck card that had the lowest `DealOrder`.

Add a test that seeds the worked hand and runs the stud `ProcessBettingActionCommandHandler` path that calls `DealRemainingStreetsAsync` for a Tollbooth game. After the action:

- Alice's seventh card is Ah, `Location` Hole, `IsVisible` false, `DealtAtPhase` SeventhStreet.
- Bob's seventh card is 3d, with the same placement.
- 2c is still community, with no player.
- 7s is still in the deck.
- Neither stack and the pot are unchanged by the runout itself. The betting action that caused the all-in may already have moved chips. Assert the runout did not move them again.
- Generic showdown's only payout is Alice for 200, and the stacks are Alice 300 and Bob 100. This hand has equal contributions, so the current single-pot payout is enough. Do not layer side pots in this test.

Add one Seven Card Stud all-in runout assertion, or keep an existing one, that the card dealt is still the next deck card. Tollbooth's branch must not change stud.

Run `TollboothChooseCardTests` and the new handler test. `ChooseCard_Furthest_ReplenishesDisplayFromDeck` must stay green without an assertion change. A normal furthest choice still replenishes from the deck and still costs 0.

## Done when

- Both the stud all-in runout and the ChooseCard all-in runout call `TollboothRunout.DealStreet` for Tollbooth streets that were not chosen.
- The worked hand gives Alice Ah and Bob 3d, leaves 2c on the display, and pays Alice 300 to Bob's 100.
- Seventh-street runout cards are face-down hole cards. Earlier runout streets are face-up board cards.
- Leftover display cards are not community cards in generic showdown.
- Stud, Razz, and Pair Pressure all-in runouts still deal from the deck.
- A Tollbooth offer with two players who can bet still accepts furthest, nearest, and deck at the current prices.
