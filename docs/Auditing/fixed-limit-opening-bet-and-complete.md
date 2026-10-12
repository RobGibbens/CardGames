# Fix: opening bets and the complete are not the fixed-limit bet

This plan is the implementation spec for one defect. An LLM should follow it exactly. Do not fix other audit findings in the same change (side pots, Follow the Queen wild-card order, Tollbooth all-in runout, bring-in action order, ace-low bring-in, odd chips).

The street limit is already stored on the betting round as `MinBet`. The live handlers do not require the bet or the complete to be that amount. Do not invent a second stake.

## Outcome

On a fixed-limit stud street, the only legal aggressive total is one amount:

- The street limit is `BettingRound.MinBet`. Third and fourth street persist the small bet there. Fifth, sixth, and seventh persist the big bet. `StudGameStakesNormalizer` sets `SmallBet` from the game's minimum bet and `BigBet` to twice that. Do not recompute those stakes in the betting handler.
- There is no bet yet, or the street bet is still below the first full bet (`CurrentBet < MinBet`, which is the bring-in, or a short all-in that never reached the limit). The only legal aggressive total is `MinBet`. That total is the opening bet and the complete. A complete raises to the small bet. It does not raise by the small bet on top of the bring-in, and it does not add another small bet in the same action.
- The street bet is already at least one full bet. The last full level is the greatest multiple of `MinBet` that is less than or equal to `CurrentBet`. The only legal aggressive total is that level plus `MinBet`. A short all-in that moved the street bet from 10 to 14 does not make the next full raise 24. The next full raise is still 20.
- A player who cannot reach that total may put in every remaining chip. That short all-in is legal. Any other total is not.
- A player who can reach that total must use that total. `AllIn` does not let them put in the rest of a larger stack.
- Check, call, and fold are unchanged. A call of the bring-in posts the bring-in amount and does not raise.

`command.Amount` is the player's new total wager on this street, not the chips added. A player with `CurrentBet` 0 who completes a bring-in of 5 up to a small bet of 10 sends 10.

Games whose live betting path must enforce this:

| Game | Handler that accepts the action |
|---|---|
| Seven Card Stud | `src/CardGames.Poker.Api/Features/Games/SevenCardStud/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Razz | That same stud handler. The web router sends Razz betting to the stud route. |
| Pair Pressure | Its handler forwards to the stud command. |
| Tollbooth | Its handler forwards to the stud command. |
| Baseball | `src/CardGames.Poker.Api/Features/Games/Baseball/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Follow the Queen | `src/CardGames.Poker.Api/Features/Games/FollowTheQueen/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |
| Good Bad Ugly | `src/CardGames.Poker.Api/Features/Games/GoodBadUgly/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` |

Good Bad Ugly has no bring-in. Its opening bet is still exactly `MinBet` on that street. The same function covers a street whose `CurrentBet` is 0.

## Leave these alone

- `BettingActionMath.ValidateRaise` and `ComputeRaise`. Hold'em, Hold the Baseball, Five Card Draw, and Twos Jacks Man With The Axe call them and are not fixed-limit stud. A raise there may be any size from the minimum raise up to the stack. Do not add an exact-limit check inside `ValidateRaise`.
- `CardGames.Poker/Betting/BettingRound.cs`. The table does not process stud actions through it. Its minimum-only bet check is not the live path.
- Dealers and flow handlers that create the round with `LastRaiseAmount = 0`. That value is the bug's starting point, and it must stay. Setting it to `MinBet` at round creation makes the first raise `CurrentBet + MinBet`. A bring-in of 5 and a small bet of 10 would then require a raise to 15 and would reject the legal complete to 10.
- `StudGameStakesNormalizer`. Bring-in, small bet, and big bet are already derived there.
- Which seat acts first, and whether that seat may raise. That is a separate defect.
- Side-pot payment at showdown.
- The chip-movement lines inside a bet, call, raise, or all-in that has already been accepted: subtract the chips moved from `ChipStack`, add them to `CurrentBet` and `TotalContributedThisHand`, and set `IsAllIn` when the stack hits 0. This change decides which totals are accepted and which round fields record a full bet.

## The legal total

Add `src/CardGames.Poker.Api/Betting/FixedLimitStudBetting.cs`.

```csharp
public static int LastFullLevel(int streetCurrentBet, int streetLimit)
{
    if (streetLimit <= 0) return 0;
    if (streetCurrentBet < streetLimit) return 0;
    return streetCurrentBet / streetLimit * streetLimit;
}

public static int NextFullTotal(int streetCurrentBet, int streetLimit)
    => LastFullLevel(streetCurrentBet, streetLimit) + streetLimit;

public static int? LegalAggressiveTotal(
    int streetCurrentBet,
    int playerCurrentBet,
    int chipStack,
    int streetLimit)
{
    if (chipStack <= 0 || streetLimit <= 0) return null;
    var maxTotal = playerCurrentBet + chipStack;
    if (maxTotal <= streetCurrentBet) return null;
    var nextFull = NextFullTotal(streetCurrentBet, streetLimit);
    return maxTotal >= nextFull ? nextFull : maxTotal;
}
```

`LegalAggressiveTotal` returns null when the player cannot put the street bet higher. They call, check, or fold. It returns `streetLimit` for an opening bet and for a complete. It returns the short-stack total when that total is above `streetCurrentBet` and below the next full level.

`IsFullLevel(total, streetLimit)` is `streetLimit > 0 && total >= streetLimit && total % streetLimit == 0`.

A rejected action uses `ProcessBettingActionErrorCode.InvalidAction` and writes nothing. No stack, pot, `CurrentBet`, or `LastRaiseAmount` change.

Validation in each of the four handlers, before `ExecuteAction`:

- `Bet` when `CurrentBet > 0`: keep the existing "use raise" error.
- `Bet` when `CurrentBet == 0`: `amount` must equal `LegalAggressiveTotal`. If the method returns null, or `streetLimit` is 0, reject. The message names the required total: `Bet must be {total}`.
- `Raise` when `CurrentBet == 0`: keep the existing "use bet" error.
- `Raise` when `CurrentBet > 0`: do not call `BettingActionMath.ValidateRaise` on these four handlers. `amount` must equal `LegalAggressiveTotal`. The message is `Raise must be to {total}`.
- `AllIn` when `ChipStack == 0`: keep the existing error.
- `AllIn` when `player.CurrentBet + player.ChipStack <= bettingRound.CurrentBet`: this is a short call. Accept it. The existing all-in arm already posts the stack without raising the street bet.
- `AllIn` when the new total would exceed `CurrentBet`: accept it only when that new total equals `LegalAggressiveTotal`. A stack that can reach the next full level and still have chips left is rejected: `All-in must be the {total} bet`. They send `Bet` or `Raise` for that total.

`ExecuteAction` for an accepted `Bet` or `Raise` keeps the current chip updates. Then:

- If the street's new `CurrentBet` is a full level, set `LastRaiseAmount = streetLimit`, set `LastAggressorIndex` to the actor's seat, and increment `RaiseCount`.
- If it is not a full level, leave `LastRaiseAmount` and `LastAggressorIndex` as they were and do not increment `RaiseCount`. Still set `CurrentBet` to the short total so the other players can call it.

The accepted `AllIn` arm uses the same marker rule when it raises. A short all-in must not take the branch that sets `LastRaiseAmount = raiseBy` merely because `raiseBy >= LastRaiseAmount`. With `LastRaiseAmount` still 0, that comparison treats a 1-chip all-in as a full raise.

Do not use `LastRaiseAmount` to compute the legal total. After a complete from 5 to 10 the increment is 5, but the next raise is 10. The marker rule stores 10 in `LastRaiseAmount` so an old `CurrentBet + LastRaiseAmount` display is not short, but the new display path below must call `LegalAggressiveTotal` anyway.

## What the table offers

The action panel slider runs from `MinRaise` (or `MinBet`) to `MaxBet` and sends that number as `command.Amount`. `MaxBet` is the player's stack today, so the slider offers 200 on a 10 street. Set the fixed-limit fields so the slider's minimum and maximum are the same legal total. Do not edit `ActionPanel.razor`.

Use `LegalAggressiveTotal` in these copies of the available-action math, and only for the fixed-limit stud codes `SEVENCARDSTUD`, `RAZZ`, `BASEBALL`, `FOLLOWTHEQUEEN`, `GOODBADUGLY`, `PAIRPRESSURE`, and `TOLLBOOTH`. Read `Game.CurrentHandGameTypeCode` when it is set, otherwise `Game.GameType.Code`.

- `TableStateBuilder.BuildAvailableActionsAsync` in `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`. Hold'em, draw, and the other non-stud phases that share this method keep `MaxBet = ChipStack` and `MinRaise = CurrentBet + (LastRaiseAmount > 0 ? LastRaiseAmount : MinBet)`.
- `SevenCardStud` `GetCurrentPlayerTurnQueryHandler.CalculateAvailableActions`. Pair Pressure and Tollbooth forward to this handler.
- `Baseball` `GetCurrentPlayerTurnQueryHandler.CalculateAvailableActions`.
- `FollowTheQueen` `GetCurrentPlayerTurnQueryHandler.CalculateAvailableActions`.

For those stud responses, when `legal` is not null:

- `CanBet` is true when `CurrentBet == 0`.
- `CanRaise` is true when `CurrentBet > 0`.
- `MinBet` is `legal` when `CurrentBet == 0`, otherwise the round's `MinBet`.
- `MinRaise` is `legal`.
- `MaxBet` is `legal`.
- `CanAllIn` is true only when `ChipStack > 0` and the player's entire stack is the legal aggressive total, or the player cannot raise (`legal` is null) and still has chips. A stack that can post the full limit does not get an all-in button.

`CanCheck`, `CanCall`, `CanFold`, and `CallAmount` stay as they are.

Good Bad Ugly has no turn query. Its panel reads `TableStateBuilder`, so the builder change is the one that covers it.

## Worked hands

Two players, Alice seat 0 and Bob seat 1, stacks 1000, unless a hand says otherwise. The betting round is the live shape: `LastRaiseAmount` is 0, `MinBet` is the street limit, and no action has been recorded yet. Assert the error path leaves `ChipStack`, `CurrentBet`, `LastRaiseAmount`, and the pot amount untouched.

1. Third street, bring-in already posted. `CurrentBet` is 5, `MinBet` is 10, Bob's `CurrentBet` is 5, Alice's is 0. Alice `Raise` 6 is rejected. Alice `Raise` 20 is rejected. Alice `Raise` 10 is accepted: Alice's stack is 990, Alice's `CurrentBet` is 10, the street `CurrentBet` is 10, `LastRaiseAmount` is 10. Bob `Call` then puts in 5 more and does not change `CurrentBet` or `LastRaiseAmount`.

2. Fourth street, no bet. `CurrentBet` is 0, `MinBet` is 10. Alice `Bet` 200 is rejected. Alice `AllIn` is rejected. Alice `Bet` 10 is accepted: stack 990, street `CurrentBet` 10, `LastRaiseAmount` 10. Bob `Raise` 15 is rejected. Bob `Raise` 20 is accepted: Bob's `CurrentBet` is 20, `LastRaiseAmount` stays 10.

3. Fifth street. `MinBet` is 20. Alice `Bet` 10 is rejected. Alice `Bet` 20 is accepted. Bob `Raise` 30 is rejected. Bob `Raise` 40 is accepted, and `LastRaiseAmount` is 20.

4. Fourth street, Carol's stack is 4 and everyone else's `CurrentBet` is 0. `MinBet` is 10. Carol `Bet` 4 is accepted and Carol `AllIn` is the same result: street `CurrentBet` is 4, Carol's stack is 0, `IsAllIn` is true, `LastRaiseAmount` stays 0, `LastAggressorIndex` stays -1. Alice `Raise` 14 is rejected. Alice `Raise` 10 is accepted.

5. Fourth street, the street bet is already a full 10 and `LastRaiseAmount` is 10. Bob has `CurrentBet` 10 and 4 chips left. Bob `AllIn` is accepted. The street `CurrentBet` becomes 14. `LastRaiseAmount` stays 10. `LastAggressorIndex` stays the seat that made the bet 10. Alice, with `CurrentBet` 0 and a large stack, `Raise` 24 is rejected and `Raise` 20 is accepted.

6. Third street, bring-in 5. Alice `Call` moves 5 chips. The street `CurrentBet` stays 5. `LastRaiseAmount` stays 0.

## Tests

Add unit tests for `FixedLimitStudBetting.LegalAggressiveTotal` and `LastFullLevel` covering hands 1 through 5's totals: `(street 5, player 0, stack 1000, limit 10)` is 10; `(street 10, player 0, stack 1000, limit 10)` is 20; `(street 14, player 0, stack 1000, limit 10)` is 20; `(street 0, player 0, stack 4, limit 10)` is 4; `(street 10, player 10, stack 4, limit 10)` is 14; `(street 5, player 5, stack 1000, limit 10)` is null.

Extend `src/Tests/CardGames.Poker.Tests/Api/Features/Games/SevenCardStud/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandlerTests.cs`. The existing `SeedGameAsync` writes `LastRaiseAmount = currentBet > 0 ? currentBet : minBet`. That is not the live round. The new tests must save `LastRaiseAmount = 0` and the `MinBet` from the hand. `Handle_RaiseAboveStack` already rejects a raise larger than the stack; leave it green.

Assert each rejected action in hands 1, 2, and 3 on the stud handler, and one accepted complete from hand 1. Assert Carol's short open and Alice's later complete from hand 4.

Add the same rejected opening bet and rejected complete to the Baseball, Follow the Queen, and Good Bad Ugly handler tests, one each. Those handlers are separate copies. A stud-only edit leaves them accepting 200. Good Bad Ugly's case is an opening `Bet` of 200 with `MinBet` 10 and `CurrentBet` 0, not a bring-in.

Add one turn-query or table-state assertion for Seven Card Stud fourth street, `CurrentBet` 0, `MinBet` 10, stack 1000, `LastRaiseAmount` 0: `CanBet` is true, `CanAllIn` is false, `MinBet` and `MaxBet` are both 10. And one third-street assertion with `CurrentBet` 5 and `MinBet` 10: `MinRaise` and `MaxBet` are both 10.

Run the Hold'em betting tests. Their raises above the minimum bet must still succeed. Do not change those assertions.

## Done when

- The four stud-family handlers accept only `LegalAggressiveTotal` for `Bet`, `Raise`, and a raising `AllIn`.
- A bring-in of 5 on a small bet of 10 can be completed to 10 and cannot be raised to 6 or to 20.
- An opening bet on a 10 street can be 10 and cannot be 200 or an all-in of a larger stack.
- Fifth street uses the big bet stored in `MinBet`.
- A short all-in does not change `LastRaiseAmount` or `LastAggressorIndex`.
- `BettingActionMath` is unchanged, and Hold'em still accepts a raise larger than the big blind.
- Round creation still persists `LastRaiseAmount = 0`.
- The stud, baseball, and Follow the Queen turn responses, and `TableStateBuilder` for the seven stud codes, advertise that single total as both the minimum and the maximum.
