# Fix: A double-submitted action or a timer race can act for the next seat

## Defect

`GameAuthorizationBehavior.EnsureCurrentActorAsync` reads the actor in a separate `AsNoTracking` query, then calls the handler. The betting and draw commands do not carry the seat that read saw. The handler loads the game again and acts for whoever `BettingRound.CurrentActorIndex` or `Game.CurrentDrawPlayerIndex` is at that later load.

`BettingRound` and `Game` use `IsRowVersion`. Two handlers that loaded the same row collide on `SaveChanges`, and the loser throws `DbUpdateConcurrencyException`. That does not cover the case this bug is. The second request loads after the first request has committed. It sees the new row version and the next seat, and the save succeeds.

The hand. Seats 0, 1, and 2 are Alice, Bob, and Carol. Each stack is 500. Blinds are posted: Alice 5, Bob 10. Phase `PreFlop`. The incomplete round's `CurrentActorIndex` is 0. Alice is the player to act. Her client sends `Fold` twice, or her action and the timer overlap.

Both HTTP requests pass `EnsureCurrentActorAsync` while that query still returns 0. The first handler folds Alice, sets `CurrentActorIndex` to 1, and commits. The second handler loads that committed round and folds Bob. Bob's `HasFolded` becomes true. He never chose an action. `CurrentActorIndex` becomes 2.

The timer does the same thing one step later. `AutoActionService` and `PerformAutoBettingActionAsync` compare the timer seat to `CurrentActorIndex` with `AsNoTracking`, then `SendBettingActionAsync` sends `ProcessBettingActionCommand(gameId, action, amount)`. That command has no seat. If Alice's fold commits after the comparison and before the handler's load, the handler folds Bob and writes `IsTimeout = false`.

`ProcessDrawCommand` for Five Card Draw and for Twos, Jacks and the Man with the Axe does the same with `CurrentDrawPlayerIndex`. `PerformAutoDrawActionAsync` compares the timer seat, then `SendDrawActionAsync` sends `new ProcessDrawCommand(context.GameId, [])`.

`ProcessDiscardCommand` does it when `PlayerSeatIndex` is null:

```csharp
var requestedSeatIndex = command.PlayerSeatIndex ?? game.CurrentDrawPlayerIndex;
```

The Irish, Phil's Mom, and Crazy Pineapple endpoints pass the seated player's seat, so a double HTTP discard names Alice both times. A call that omits the seat, including the in-process tests that omit it, still acts for whoever the index is after the first discard commits.

## Do not change

- `FoldDuringDrawCommandHandler`. It already returns when `command.PlayerSeatIndex != game.CurrentDrawPlayerIndex` before it sets `HasFolded`.
- `DrawCardsCommandHandler`. It already returns when the `PlayerId` on the command is not the seat in `CurrentDrawPlayerIndex`.
- `HandSettlementService`, pot awarding, and chip math inside `ExecuteAction`.
- `CreditForCashOutAsync` and the cashier ledger.
- `ChooseCardCommandHandler` and `SelectShowcaseCommandHandler`. They have a seat fallback, and they are not `IRequiresCurrentActor`. Leave them.
- `DeckDrawCommandHandler`. It does not select a player from `CurrentActorIndex`.
- The `IsRowVersion` tokens. Do not catch `DbUpdateConcurrencyException` in a betting or draw handler and run the action again. A retry would load the next seat. The existing catches in showdown, table settings, and Bob Barker showcase stay as they are.
- `AutoActionService`'s stale-timer return, and the same return in `PerformAutoBettingActionAsync` and `PerformAutoDrawActionAsync`. Those reads are early exits. They are not the check that guards the write.

## Bind the seat to the command

Add this interface in `src/CardGames.Poker.Api/Infrastructure/AuthorizationMarkers.cs`:

```csharp
public interface IBoundToActorSeat
{
	int? BoundSeat { get; set; }
}
```

Implement it on every command that implements `IRequiresCurrentActor`, with a mutable property that is not a constructor parameter:

```csharp
public int? BoundSeat { get; set; }
```

Existing `new ProcessBettingActionCommand(gameId, action, amount)` and `new ProcessDrawCommand(gameId, discards)` calls keep compiling. `BoundSeat` stays null until something sets it.

Commands:

- `HoldEm`, `FiveCardDraw`, `TwosJacksManWithTheAxe`, `SevenCardStud`, `GoodBadUgly`, `Baseball`, `FollowTheQueen`, and `HoldTheBaseball` `ProcessBettingActionCommand`
- `PairPressure` and `Tollbooth` `ProcessBettingActionCommand`
- `FiveCardDraw` and `TwosJacksManWithTheAxe` `ProcessDrawCommand`
- `IrishHoldEm` `ProcessDiscardCommand` and `FoldDuringDrawCommand`
- `KingsAndLows` `DrawCardsCommand` and `DeckDrawCommand`

`FoldDuringDraw`, `DrawCards`, and `DeckDraw` get the property so the behavior can set it. Their handlers do not grow a second seat check.

In `EnsureCurrentActorAsync`, on every path that is about to call `next`, including the path that returns when `actorSeat` is null, set:

```csharp
if (request is IBoundToActorSeat bound)
	bound.BoundSeat = seated.SeatPosition;
```

`seated` is the authenticated caller already resolved above. Overwrite any value that arrived on the command. A caller who is not the actor still throws `NotPlayerTurn` before this assignment when `actorSeat` has a value. Do not change `ResolveActorSeatAsync`. That query is still only the early 403. It does not filter `HandNumber`, and it is not the seat the handler acts for.

The timer has no `HttpContext`, so the behavior returns at the top of `Handle` and does not clear `BoundSeat`. The auto-action that creates the command sets it.

## Refuse before any chip or card moves

Add `src/CardGames.Poker.Api/Features/Games/Common/ActorSeatBinding.cs`:

```csharp
internal static class ActorSeatBinding
{
	internal static bool IsStale(int? boundSeat, int liveSeat) =>
		boundSeat is null || boundSeat.Value != liveSeat;
}
```

Call it in each handler below immediately after that handler has chosen `currentPlayer` or `currentDrawPlayer` from the tracked row, and before `ExecuteAction`, before a card is discarded or dealt, and before `CurrentActorIndex` or `CurrentDrawPlayerIndex` is written. A stale command returns the handler's existing not-your-turn error and does not call `SaveChanges`.

| Handler | Live seat | Error already used for a missing actor |
|---|---|---|
| Each `ProcessBettingActionCommandHandler` listed above, including Seven Card Stud | `bettingRound.CurrentActorIndex` | The existing `InvalidGameState` / "Could not determine current player." return stays when `currentPlayer` is null. The stale check runs only after `currentPlayer` is found. Return that same error type with message `It is not your turn.` |
| `FiveCardDraw` and `TwosJacksManWithTheAxe` `ProcessDrawCommandHandler` | `game.CurrentDrawPlayerIndex` | Keep the existing return when the index is `< 0` or the player is missing. Then `ActorSeatBinding.IsStale(command.BoundSeat, currentDrawPlayer.SeatPosition)` returns the existing `NotPlayerTurn` error. |
| `ProcessDiscardCommandHandler` | `game.CurrentDrawPlayerIndex` | Delete `command.PlayerSeatIndex ?? game.CurrentDrawPlayerIndex`. If `command.PlayerSeatIndex` is null, or it is not `game.CurrentDrawPlayerIndex`, return `NotPlayerTurn` before selecting cards. The endpoint already passes `seated.SeatPosition`. |

`PairPressure` and `Tollbooth` `ProcessBettingActionCommandHandler` call `innerHandler.Handle` with a new Seven Card Stud command and do not go through MediatR. Copy the seat:

```csharp
var inner = new SharedProcessBettingActionCommand(command.GameId, command.ActionType, command.Amount)
{
	BoundSeat = command.BoundSeat
};
return innerHandler.Handle(inner, cancellationToken);
```

Do not `mediator.Send` the inner command.

## Timer commands

Set `BoundSeat = context.PlayerSeatIndex` on the command these methods send. `context.PlayerSeatIndex` is the seat the timer was started for. Do not replace it with a `CurrentActorIndex` read done while building the command.

- `BaseGameFlowHandler.SendBettingActionAsync`
- `BaseGameFlowHandler.SendDrawActionAsync`
- Every override of `SendBettingActionAsync`: `HoldEmFlowHandler`, `OmahaFlowHandler`, `NebraskaFlowHandler`, `RedRiverFlowHandler`, `SouthDakotaFlowHandler`, `IrishHoldEmFlowHandler`, `CrazyPineappleFlowHandler`, `PhilsMomFlowHandler`, `BobBarkerFlowHandler`, `KlondikeFlowHandler`, `HoldTheBaseballFlowHandler`, `SevenCardStudFlowHandler`, `FollowTheQueenFlowHandler`, `GoodBadUglyFlowHandler`, `BaseballFlowHandler`, `PairPressureFlowHandler`, `TollboothFlowHandler`

`KingsAndLowsFlowHandler.SendDrawActionAsync` keeps sending `DrawCardsCommand` with that seat's `PlayerId`. The draw handler already rejects a player who is no longer the draw index. Do not add a `BoundSeat` check there.

`FoldDuringDrawCommand` is already constructed with `context.PlayerSeatIndex`. Leave that comparison in place.

## Worked hand

Alice seat 0, Bob seat 1, Carol seat 2. Stacks 500. Alice has posted 5, Bob has posted 10, Carol has posted nothing. `CurrentActorIndex` is 0. Alice sends Fold. `BoundSeat` is 0.

The first handler loads actor 0, folds Alice, sets `CurrentActorIndex` to 1, and saves. One `BettingActionRecord`: Alice, Fold, `IsTimeout` false.

The second Fold is still Alice's request, so `BoundSeat` is 0. It loads actor 1. `ActorSeatBinding.IsStale(0, 1)` is true. The handler returns `It is not your turn.` Bob's `HasFolded` stays false. His stack stays 490. `CurrentActorIndex` stays 1. There is still one action record.

The timer that was started for seat 0 fires after that commit. Its command has `BoundSeat` 0. The same return happens. Bob is not folded, and no timeout action is written.

The same numbers with the draw. `CurrentDrawPlayerIndex` is 0. Alice's stand-pat commits and the index becomes 1. A second `ProcessDrawCommand` with `BoundSeat` 0 returns `NotPlayerTurn`. Bob's cards are unchanged.

Two handlers that both load actor 0 and both see `BoundSeat` 0 still collide on `IsRowVersion`. The second `SaveChanges` throws. Do not catch that and fold Bob.

A Fold from Bob, once he is the actor, has `BoundSeat` 1 and applies to Bob.

## Tests

`GameAuthorizationBehaviorTests.Betting_WhenAuthenticatedPlayerIsCurrentActor_Continues` asserts the command's `BoundSeat` is 0 before `next` runs. `Betting_WhenAuthenticatedPlayerIsNotCurrentActor_ReturnsNotPlayerTurn` still throws, and `BoundSeat` stays null. `Betting_WithoutHttpContext_AllowsTimerAutoAction` still calls `next` and does not change `BoundSeat`.

Add a Hold'em handler test next to `src/Tests/CardGames.Poker.Tests/Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/PerformShowdownCommandHandlerTests.cs`, in a new `ProcessBettingAction` test class if one does not exist. Seed the worked hand: actor index 0, Alice folded is not required for the stale case. Call `Handle` with Fold and `BoundSeat` 1 while `CurrentActorIndex` is 0. Assert the error message `It is not your turn.`, Alice's `HasFolded` is false, and no `BettingActionRecord` was added. Call it again with `BoundSeat` null and assert the same refusal. Call it with `BoundSeat` 0 and assert Alice folds and `CurrentActorIndex` is the next seat.

Add the same stale-seat assertion to `FiveCardDraw` `ProcessDrawCommandHandler`: `CurrentDrawPlayerIndex` 0, `BoundSeat` 1, no cards discarded.

Add one `ProcessDiscard` test: `PlayerSeatIndex` null, `CurrentDrawPlayerIndex` 0, result `NotPlayerTurn`, no cards discarded. The existing Irish, Phil's Mom, and Crazy Pineapple calls that omit `PlayerSeatIndex` and expect a discard must pass the current draw seat. `IrishHoldEmHandLifecycleTests` already has a call that passes `nonCurrentSeat`. That call must still be `NotPlayerTurn`.

In-process tests and auto-actions that send these commands without HTTP do not get `BoundSeat` from the behavior. Set `BoundSeat` to the seat the test means to act for. A null seat is a refusal. Do not relax `IsStale` so an existing test stays green.

Run:

```text
dotnet test src/Tests/CardGames.Poker.Tests/CardGames.Poker.Tests.csproj --filter "FullyQualifiedName~ProcessBettingAction|FullyQualifiedName~ProcessDraw|FullyQualifiedName~ProcessDiscard|FullyQualifiedName~GameAuthorizationBehaviorTests"
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~GameAuthorizationBehaviorTests|FullyQualifiedName~IrishHoldEm|FullyQualifiedName~CrazyPineapple|FullyQualifiedName~PhilsMom"
```

## Done when

- Alice's second Fold, and the timer's Fold for seat 0, leave Bob unfolded with `CurrentActorIndex` still 1.
- A `ProcessDrawCommand` whose `BoundSeat` is not `CurrentDrawPlayerIndex` changes no cards.
- `ProcessDiscardCommand` with a null seat does not discard for `CurrentDrawPlayerIndex`.
- Bob's own Fold, with `BoundSeat` 1 while he is the actor, still folds Bob.
- A handler that lost the row-version race is not retried.
