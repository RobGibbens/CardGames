# Fix: The preflop minimum raise is not enforced

## Defect

`docs/TexasHoldEmRules.md` says the big blind is a live bet. With a big blind of 2, a call costs 2 and a raise must be to at least 4. The raise increment is the big blind.

`BettingActionMath.ValidateRaise` already enforces that. The minimum raise-to is `CurrentBet + LastRaiseAmount`. A short all-in, where the player's total equals `CurrentBet + ChipStack` and that total is below the minimum, is allowed. Any other amount below the minimum is rejected.

The preflop round is created in `BaseGameFlowHandler.DealDrawStyleCardsAsync` with `LastRaiseAmount = 0`. Blind games take that path: `SkipsAnteCollection` is true, blinds are posted, and `CurrentBet` on the new round is the largest posted `CurrentBet`, which is the big blind. The first voluntary action is a raise of that live bet.

`GetMinRaiseTo(bigBlind, 0)` is the big blind itself. The only amount `ValidateRaise` rejects is an amount that does not exceed the current bet. A raise to `bigBlind + 1` is accepted.

`TableStateBuilder` displays `MinRaise` as `CurrentBet + (LastRaiseAmount > 0 ? LastRaiseAmount : MinBet)`. The server does not use that fallback. The client can show a minimum the handler does not enforce.

Postflop rounds created in `HoldEm/.../ProcessBettingActionCommandHandler` already set `LastRaiseAmount` to `game.BigBlind ?? game.MinBet ?? 0`. The first postflop action is a bet, not a raise, and a bet replaces `LastRaiseAmount` with the bet size. That path is not this defect.

The hand. Blinds 1 and 2. Button Alice, small blind Bob, big blind Carol. Each stack starts at 100. After the blinds, Bob has stack 99 and `CurrentBet` 1. Carol has stack 98 and `CurrentBet` 2. The round's `CurrentBet` is 2 and `LastRaiseAmount` is 0. Dave, first to act, has stack 100 and `CurrentBet` 0.

Dave raises to 3. Today the handler accepts it. He puts in 3. His stack is 97. The round's `CurrentBet` is 3. `LastRaiseAmount` becomes 1, because the raise branch assigns `LastRaiseAmount = raiseIncrement` even when the increment is smaller than the previous minimum.

The legal minimum is a raise to 4. Dave puts in 4. His stack is 96. `CurrentBet` is 4. The increment is 2, so `LastRaiseAmount` stays 2. The next raise must be to at least 6.

## Do not change

- `BettingActionMath.ValidateRaise` and `ComputeRaise`. A raise that does not exceed `CurrentBet` is still illegal. A raise above the player's stack is still illegal. A total that equals the player's whole stack is still legal when it is short of the minimum.
- The postflop round created when the street advances. `MinBet` and `LastRaiseAmount` there are already `game.BigBlind ?? game.MinBet ?? 0`, and `CurrentBet` is 0.
- `BettingActionType.Bet`. An opening bet on a street with `CurrentBet` 0 must still be at least `MinBet`, and it still sets `LastRaiseAmount` to that bet.
- `BettingActionType.AllIn`. It already updates `LastRaiseAmount`, `LastAggressorIndex`, and `RaiseCount` only when `raiseBy >= LastRaiseAmount`.
- `BettingRoundCompletion`. A short all-in does not become `LastAggressorIndex`, so it does not reopen the round by itself.
- `MaxRaises`. Nothing in the Hold'em handler reads it. Do not add a cap.
- Stud, draw, Good Bad Ugly, Baseball, Follow the Queen, and Twos/Jacks betting handlers. Their opening rounds are not a live big blind. Do not copy this assignment into those handlers.
- Kings and Lows and Screw Your Neighbor. They set `SkipsAnteCollection` and they do not call `DealDrawStyleCardsAsync`.
- Side pots, showdown, and the cashier.

## Opening increment

In `DealDrawStyleCardsAsync`, replace the hard-coded `LastRaiseAmount = 0` on the first `BettingRound`.

When `isBlindBasedHand` is true and `openingCurrentBet` is greater than 0, set `LastRaiseAmount` to the first of these that is greater than 0:

1. `game.BigBlind`
2. `game.MinBet`
3. `openingCurrentBet`

Otherwise leave `LastRaiseAmount` at 0. An ante game, and a blind game whose posted bets are all 0, still open with 0. The first bet on those rounds is a `Bet`, which writes the increment itself.

`CurrentBet` on that round stays `openingCurrentBet`. Do not change `MinBet` on this round. Preflop action raises the live blind. It does not open with `Bet`.

This one assignment covers every game that deals through `DealDrawStyleCardsAsync` and then bets through `HoldEm/.../ProcessBettingActionCommandHandler`: Hold'em, Red River, Omaha, Nebraska, South Dakota, Bob Barker, Irish Hold'em, Phil's Mom, Crazy Pineapple, Hold the Baseball, and Klondike.

## A short all-in must not shrink the minimum

In that same Hold'em handler, the `BettingActionType.Raise` arm always assigns:

```csharp
bettingRound.LastRaiseAmount = raiseIncrement;
bettingRound.LastAggressorIndex = player.SeatPosition;
bettingRound.RaiseCount++;
```

Use the condition the `AllIn` arm already uses. Update those three fields only when `raiseIncrement >= bettingRound.LastRaiseAmount`. Always update `CurrentBet`, the player's stack, `CurrentBet`, `TotalContributedThisHand`, and `IsAllIn`.

A raise to 4 over a blind of 2 has increment 2. `2 >= 2`, so `LastRaiseAmount` becomes 2. A later raise to 10 has increment 6. `6 >= 2`, so `LastRaiseAmount` becomes 6. The next minimum raise-to is 16.

A raise to 3 by a player whose stack is 3 has increment 1. `1 >= 2` is false. The action is accepted. `CurrentBet` becomes 3. `LastRaiseAmount` stays 2. `LastAggressorIndex` and `RaiseCount` stay as they were. The next full raise must still be to at least 5.

## Worked hands

Blinds 1 and 2. Four players, stacks 100 before blinds. Bob has posted 1. Carol has posted 2. Dave acts first. The new round has `CurrentBet` 2 and `LastRaiseAmount` 2.

### 1. Raise to 3

`ValidateRaise` returns `Raise must be to at least 4`. Dave's stack stays 100. `CurrentBet` stays 0. The round's `CurrentBet` stays 2. No `BettingActionRecord` is added.

### 2. Raise to 4

Accepted. Dave's stack is 96. His `CurrentBet` is 4. He has contributed 4 this hand, plus nothing else. The round's `CurrentBet` is 4. `LastRaiseAmount` is 2. `LastAggressorIndex` is Dave's seat.

### 3. Short all-in

Dave's stack is 3 and his `CurrentBet` is 0. He raises to 3. Accepted. His stack is 0. `IsAllIn` is true. The round's `CurrentBet` is 3. `LastRaiseAmount` stays 2. `LastAggressorIndex` stays -1, which is how this round is created. The next player may call 3 or raise to at least 5.

### 4. Raise after a full raise

The round is at `CurrentBet` 4 and `LastRaiseAmount` 2. Alice raises to 10. Increment 6. Accepted. `LastRaiseAmount` becomes 6. `CurrentBet` becomes 10. A following raise to 15 is rejected. A following raise to 16 is accepted.

### 5. Postflop is unchanged

The flop round is created with `CurrentBet` 0, `MinBet` 2, and `LastRaiseAmount` 2. The first action is a bet. A bet of 2 is legal. It sets `LastRaiseAmount` to 2. A bet of 1 is still rejected by the `MinBet` check.

## Tests

`BettingActionMathTests` already rejects a raise to 30 when the street bet is 20 and `LastRaiseAmount` is 20. Add two rows to `ValidateRaise_MatchesStackAndStreetRules`:

- amount 3, street bet 2, last raise 2, player bet 0, stack 100, error, message contains `at least 4`
- amount 4, street bet 2, last raise 2, player bet 0, stack 100, no error

Add three tests to `ProcessBettingActionCommandHandlerTests`:

1. Preflop round, `CurrentBet` 2, `LastRaiseAmount` 2, Dave's stack 100 and `CurrentBet` 0. Raise to 3 returns `InvalidAction`. Stack, `CurrentBet`, and `LastRaiseAmount` are unchanged.
2. The same round. Raise to 4 succeeds. Dave's `CurrentBet` is 4. The round's `LastRaiseAmount` is 2.
3. Dave's stack is 3. Raise to 3 succeeds. The round's `CurrentBet` is 3. `LastRaiseAmount` stays 2. `LastAggressorIndex` stays at the value seeded on the round.

`Handle_ShortAllInRaiseBelowMinRaise_Succeeds` seeds `LastRaiseAmount` equal to `CurrentBet` (20) and raises to 25. That remains a successful short all-in. Assert `LastRaiseAmount` is still 20 after it. Today the raise arm sets it to 5.

Add one deal test that runs `HoldEmFlowHandler.DealCardsAsync` for a game with `SmallBlind` 1, `BigBlind` 2, and at least three seated players who can post. The created incomplete round has `CurrentBet` 2 and `LastRaiseAmount` 2. Do not assert that value by seeding the round in the handler test and skipping the deal. The handler test proves enforcement. The deal test proves the live round is created with the increment the handler enforces.

Run:

```text
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~BettingActionMathTests|FullyQualifiedName~ProcessBettingActionCommandHandlerTests|FullyQualifiedName~HoldEmFlowHandler"
```

## Done when

- A preflop round dealt with a big blind of 2 has `LastRaiseAmount` 2.
- A raise to 3 is rejected. A raise to 4 is accepted, and the next minimum raise-to is 6.
- An all-in raise to 3 is accepted and leaves `LastRaiseAmount` at 2.
- A postflop opening bet is still checked against `MinBet`, and a full raise still replaces `LastRaiseAmount` with that raise's increment.
