# Fix: the bring-in seat is first to act and can raise

This plan is the implementation spec for one defect. An LLM should follow it exactly. Do not fix other audit findings in the same change (side pots, Follow the Queen wild-card order, fixed-limit bet size, Tollbooth all-in runout, ace-low bring-in, odd chips).

The live rule is the one already implemented by `CardGames.Poker/Betting/BettingRound.cs` when it is constructed with a forced bet: that player has acted, and the first voluntary action belongs to the next player. The table does not use that class. Do not route the API through it.

## Outcome

On third street, after a bring-in greater than zero has been posted:

- `Game.BringInPlayerIndex` is the seat that posted. It is not the seat to act.
- `Game.CurrentPlayerIndex` and `BettingRound.CurrentActorIndex` are the next seat to the left that can act. A folded seat and an all-in seat are skipped. The bring-in seat is skipped.
- `BettingRound.LastAggressorIndex` is the bring-in seat.
- `BettingRound.PlayersActed` is 1 when that player is not all-in. It is 0 when the bring-in took their last chips. They are not in the set of players who can act, and counting them would close the round before the other players call.
- `BettingRound.CurrentBet` stays the amount posted.
- If every other player calls or folds, the round ends when the action would return to the bring-in seat. That player does not get a turn and cannot raise.
- If any other player raises, the bring-in seat acts when the action reaches them. A call or a raise from them is then legal. This plan does not change how large that raise must be.

If the bring-in amount is zero, or the low card has no chips and nothing is posted, third street is unchanged: the low card opens, `LastAggressorIndex` stays -1, and `PlayersActed` stays 0.

Fourth street and later still open on the best visible hand. This change does not move that seat.

## Leave these alone

- Which upcard is lowest. Ace-low bring-in in Follow the Queen is a separate defect. The worked hand uses a jack and a king.
- The size of a complete or an opening bet. Fixed-limit sizing is a separate plan. A raise to the street `MinBet` is legal both before and after that plan when the bring-in is smaller than `MinBet`.
- Good Bad Ugly. Its flow sets `BringInPlayerIndex` to -1 and its deal starts at the seat left of the dealer. Do not add a bring-in skip there.
- `CardGames.Poker/Betting/BettingRound.cs`. It already marks the forced bet as acted and starts one seat later. No API handler calls it.
- `StudBringInPosting.PostBringInForThirdStreetAsync` chip movement. It already sets `BringInPlayerIndex` to the poster, subtracts the chips, and marks all-in when the stack hits zero.

## Why the poster can raise

`StudBringInPosting` posts the chips and sets `CurrentBet` on that player to the amount posted. The street `CurrentBet` is that same amount, so the amount to call is zero. The betting handler then allows a raise whenever `CurrentBet > 0` and the player has chips above the call.

The seat stored as the actor is the poster:

- `SevenCardStudFlowHandler.DealCardsAsync` sets `CurrentPlayerIndex`, `CurrentActorIndex`, and `BringInPlayerIndex` to `bringInSeatPosition`, `LastAggressorIndex` to -1, and `PlayersActed` to 0. Razz uses this handler through `RazzFlowHandler`.
- `BaseballFlowHandler`, `FollowTheQueenFlowHandler`, `PairPressureFlowHandler`, and `TollboothFlowHandler` do the same. Pair Pressure sets `LastAggressorIndex` to the bring-in seat when chips were posted, and still sets the actor to that seat. Follow the Queen leaves `LastAggressorIndex` at the entity default of -1.
- `SevenCardStud` `DealHandsCommandHandler` sets `firstActorSeatPosition` to `FindBringInPlayer` and stores that seat as `CurrentActorIndex`, `CurrentPlayerIndex`, and `BringInPlayerIndex`. `LastAggressorIndex` is -1. `PlayersActed` is 0. Pair Pressure and Tollbooth deal handlers forward to this handler. Razz continuous play uses the stud flow, not a separate deal handler.
- `Baseball` `DealHandsCommandHandler` and `FollowTheQueen` `DealHandsCommandHandler` repeat that assignment.

`IsRoundComplete` in the stud betting handler closes the round when the next seat is `LastAggressorIndex` and every player who can act has matched, or when `PlayersActed` has reached that same set and they have matched. With `LastAggressorIndex` at -1 and `PlayersActed` at 0, a call does not close the round. The next seat is the poster, and the raise check accepts their raise.

## One helper

Add the decision next to `StudBringInPosting`, in `src/CardGames.Poker.Api/Features/Games/Common/StudBringInPosting.cs` or a type beside it.

`FirstActorAfterBringIn(players, bringInSeat, amountPosted)` returns the actor seat, the last-aggressor seat, `PlayersActed`, and whether the round is already complete.

`players` is every active player at the table, including folded players, so an empty seat number still takes a step. Walk seats the way `ProcessBettingActionCommandHandler.FindNextActivePlayer` walks them:

```csharp
var totalSeats = players.Max(p => p.SeatPosition) + 1;
var seat = (bringInSeat + 1) % totalSeats;
```

Repeat at most `totalSeats` times. A candidate must exist, must not be folded, must not be all-in, and must not be `bringInSeat`. Then advance `seat` by one modulo `totalSeats`. Do not use a compact list index. An empty seat is a step, matching later betting.

When `amountPosted <= 0`:

- Actor is `bringInSeat`.
- `LastAggressorIndex` is -1.
- `PlayersActed` is 0.
- The round is not complete.

When `amountPosted > 0`:

- `LastAggressorIndex` is `bringInSeat`.
- `PlayersActed` is 1 when the bring-in player is not all-in, and 0 when they are.
- Actor is the first candidate of the walk.
- If the walk finds nobody, the actor is -1 and the round is complete. Set `IsComplete` and `CompletedAt`, and set `CurrentPlayerIndex` to -1. Do not leave the table waiting on the poster.

Every creator below calls this helper after `PostBringInForThirdStreetAsync` returns. They do not each inline a different seat calculation.

Flow handlers, in the third-street round they insert:

- `src/CardGames.Poker.Api/GameFlow/SevenCardStudFlowHandler.cs`
- `src/CardGames.Poker.Api/GameFlow/BaseballFlowHandler.cs`
- `src/CardGames.Poker.Api/GameFlow/FollowTheQueenFlowHandler.cs`
- `src/CardGames.Poker.Api/GameFlow/PairPressureFlowHandler.cs`
- `src/CardGames.Poker.Api/GameFlow/TollboothFlowHandler.cs`

Deal handlers, only on the third-street branch, after the bring-in post and before the betting round is saved:

- `src/CardGames.Poker.Api/Features/Games/SevenCardStud/v1/Commands/DealHands/DealHandsCommandHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/Baseball/v1/Commands/DealHands/DealHandsCommandHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/DealHands/DealHandsCommandHandler.cs`

Assign `CurrentActorIndex` and `CurrentPlayerIndex` from the helper's actor. Assign `LastAggressorIndex` and `PlayersActed` from the helper. Leave `BringInPlayerIndex` as the poster. `StudBringInPosting` already writes that. The deal handlers currently overwrite it with `firstActorSeatPosition`. Stop overwriting it with the actor. On third street it remains the low-card seat. On later streets it remains -1.

Do not change `FindBestVisibleHandPlayer` or the fourth-street and later branch.

The betting handlers do not need a new rule. Once the round is stored this way, `IsRoundComplete` closes a called bring-in when the next seat is the last aggressor, and a later raise still reaches the poster because the aggressor is then the raiser.

## Worked hand

Seats 0 and 1. Dealer is Lynne, seat 1. Rob is seat 0. Each started with 1000 and posted an ante of 5, so each stack is 995 and the pot is 10 before the bring-in. Small bet is 10. Bring-in is 5.

Third-street upcards: Rob Kh, Lynne Jc. Lynne has the lowest upcard and posts 5. Lynne's stack is 990. Lynne's `CurrentBet` is 5. Her contribution this hand is 10. The pot is 15. The street `CurrentBet` is 5. `BringInPlayerIndex` is 1.

Rob is first to act. `CurrentPlayerIndex` and `CurrentActorIndex` are 0. `LastAggressorIndex` is 1. `PlayersActed` is 1.

Rob calls 5. His stack is 990. The street bet stays 5. The next seat is Lynne, who is the last aggressor, and both players who can act have matched. The round is complete. Lynne is not asked to act. A raise command bound to seat 1 returns not-your-turn. Lynne's stack stays 990.

The same deal, with Rob raising to 10 instead of calling: the raise is accepted, the street `CurrentBet` is 10, `LastAggressorIndex` is 0, and the next actor is Lynne. Lynne is allowed to call or raise. This is the action she does not get when nobody raises.

Three-handed check of the same fields. Seats 0, 1, and 2, dealer seat 2, bring-in posted by seat 0. The first actor is seat 1. After seat 1 calls and seat 2 calls, the next seat is 0, the last aggressor is 0, and the round is complete. Seat 0 does not act.

A zero bring-in does not use this skip. The low card remains the actor, and `LastAggressorIndex` stays -1.

## Tests

Unit-test `FirstActorAfterBringIn`:

- Posted 5, seats 0 and 1, poster seat 1, neither all-in: actor 0, last aggressor 1, `PlayersActed` 1, not complete.
- Posted 5, poster seat 0, seats 0, 1, and 2: actor 1, last aggressor 0, `PlayersActed` 1.
- Posted 5, poster is all-in: actor is the next live seat, last aggressor is the poster, `PlayersActed` is 0.
- Posted 5, every other seat is folded or all-in: actor -1, round complete.
- Posted 0: actor is the poster, last aggressor -1, `PlayersActed` 0, not complete.

Update the third-street deal tests that currently require the low card to act. In each, Lynne's jack is the bring-in and Rob is seat 0:

- `src/Tests/CardGames.IntegrationTests/Features/Commands/SevenCardStudDealHandsCommandHandlerTests.cs` `DealHands_ThirdStreet_UsesLowestUpCardForFirstActor`
- The same test in `BaseballDealHandsCommandHandlerTests.cs`
- The same test in `FollowTheQueenDealHandsCommandHandlerTests.cs`

`CurrentPlayerIndex` and `CurrentPlayerName` become Rob and seat 0. `BringInPlayerIndex` stays Lynne. Fourth-street tests in those files still expect the best visible hand and must not change.

`SevenCardStudBringInTests.DealHands_ThirdStreet_PostsBringInIntoPot` keeps the chip and pot assertions. `CurrentActorIndex` becomes the other seat. Assert `LastAggressorIndex` is the poster and `PlayersActed` is 1.

`FollowTheQueen` `DealHandsCommandHandlerTests.Handle_ThirdStreet_PostsBringInIntoPotAndContribution` currently expects `CurrentActorIndex` to be the poster. Point it at the next live seat. Keep the chip and pot assertions.

Add one stud `ProcessBettingAction` test seeded as the worked heads-up round, not by re-dealing. Rob's call completes the round and does not change Lynne's stack. A second action from Lynne is not her turn. A second case, Rob's raise to 10, leaves the next actor as Lynne.

Run the fourth-street deal tests and `DealHands_ThirdStreet_ShortStack_PostsRemainderAndMarksAllIn`. The short stack still posts the remainder and is all-in. The other player is the actor. `PlayersActed` is 0.

## Done when

- The five flow handlers and the three deal handlers call `FirstActorAfterBringIn` whenever third street posts a bring-in.
- `BringInPlayerIndex` stays the poster.
- On the worked hand Rob acts, Lynne does not get a raise unless Rob raises, and Lynne's call-around stack stays 990.
- A posted all-in bring-in does not increment `PlayersActed`.
- Fourth street still starts on the best visible hand.
- Good Bad Ugly's first actor is unchanged.
