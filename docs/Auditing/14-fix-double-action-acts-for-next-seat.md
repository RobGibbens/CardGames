# Fix: a second betting action or the timer acts for the next seat

Implement this document only. Do not change chip math, settlement, or any showdown.

## What is wrong

A betting command has no seat. `GameAuthorizationBehavior.EnsureCurrentActorAsync` reads the current seat with `AsNoTracking` and then returns. The handler loads the game again and acts for `BettingRound.CurrentActorIndex` at that later load. `IsTimeout` stays `false`.

Two folds for the same hand can both commit when they do not share one snapshot. The first fold advances the actor and commits. The second load sees the new row version and the next seat, so it folds that next player and records the fold as a human action. The same thing happens when the action timer fires for the seat it was started for and a human action has already moved the actor.

A save that still holds the old `Game`, `GamePlayer`, or `BettingRound` row version throws `DbUpdateConcurrencyException`. That collision is correct. Do not catch it and do not retry it.

## Worked hand

Texas Hold'em. Three players, preflop, no bet yet. `CurrentActorIndex` is 0.

| Seat | Player | Stack | Hole cards |
| --- | --- | --- | --- |
| 0 | Alice | 1000 | As Kh |
| 1 | Bob | 1000 | 2c 7d |
| 2 | Carol | 1000 | Qh Jd |

Alice's client sends Fold. That save commits: Alice `HasFolded` is true, her stack is still 1000, `CurrentActorIndex` is 1, and one `BettingAction` row exists with `IsTimeout` false.

Alice's client sends Fold again, or the timer that was started for seat 0 sends Fold. The handler loads after the first commit.

Today the second call folds Bob, writes a second action with `IsTimeout` false, and sets `CurrentActorIndex` to 2.

After this fix the second call returns `ProcessBettingActionErrorCode.NotPlayerTurn` with message `It is not your turn.` Bob stays unfolded. `CurrentActorIndex` stays 1. The action table still has one row.

## The seat the command is allowed to act for

Add this interface in `src/CardGames.Poker.Api/Infrastructure/AuthorizationMarkers.cs`:

```csharp
public interface IBoundToActorSeat
{
	int? BoundSeat { get; set; }
}
```

`BoundSeat` is the seat this command may act for. Null means no seat was approved.

Add `IBoundToActorSeat` and a mutable property to every command that already implements `IRequiresCurrentActor`. The property is not a constructor parameter. Existing `new ProcessBettingActionCommand(gameId, action, amount)` calls must keep compiling. Callers set the seat with an object initializer: `{ BoundSeat = seat }`.

```csharp
public int? BoundSeat { get; set; }
```

Commands:

- `HoldEm`, `FiveCardDraw`, `TwosJacksManWithTheAxe`, `SevenCardStud`, `GoodBadUgly`, `Baseball`, `FollowTheQueen`, `HoldTheBaseball`, `PairPressure`, and `Tollbooth` `ProcessBettingActionCommand`
- `FiveCardDraw` and `TwosJacksManWithTheAxe` `ProcessDrawCommand`
- `IrishHoldEm` `ProcessDiscardCommand` and `FoldDuringDrawCommand`
- `KingsAndLows` `DrawCardsCommand` and `DeckDrawCommand`

`PairPressure` and `Tollbooth` betting handlers construct a new Seven Card Stud command and call `innerHandler.Handle`. Copy the seat onto that inner command. Do not send it through MediatR.

```csharp
new SharedProcessBettingActionCommand(command.GameId, command.ActionType, command.Amount)
{
	BoundSeat = command.BoundSeat
}
```

## Stamp the seat in the authorization behavior

`GameAuthorizationBehavior.Handle` returns `next()` immediately when `HttpContext` is null. Leave that return as it is. A timer has no HTTP context, and this return must not clear a `BoundSeat` the timer already set.

Change `EnsureCurrentActorAsync` so it receives `TRequest request`. It still does not run when `HttpContext` is null.

Inside `EnsureCurrentActorAsync`, after the seated player is resolved and the game row exists:

- When `ResolveActorSeatAsync` returns null, or returns the seated player's `SeatPosition`, set `((IBoundToActorSeat)request).BoundSeat = seated.SeatPosition` and return. This overwrites any seat the client put on the command.
- When the resolved seat is different from `seated.SeatPosition`, throw the existing `GameAuthorizationException` (`NotPlayerTurn`, 403) and do not change `BoundSeat`.
- When the game row does not exist, return without setting `BoundSeat`. The handler still returns its game-not-found error.
- When the caller is not seated, throw the existing not-seated exception and do not set `BoundSeat`.

`ResolveActorSeatAsync` stays as it is. Betting commands still read `BettingRound.CurrentActorIndex`. Every other `IRequiresCurrentActor` command still reads `Game.CurrentDrawPlayerIndex`.

## Refuse a stale seat before any chip or card change

Add this helper beside each handler that needs the check. Do not put it in a shared base class.

```csharp
private static bool IsStale(int? boundSeat, int liveSeat) =>
	boundSeat is null || boundSeat.Value != liveSeat;
```

Null is stale. A command that reached the handler without an approved seat must not act for whoever is current.

### Betting handlers

In each of these handlers, resolve `currentPlayer` exactly as the handler does today. The existing `InvalidGameState` / `Could not determine current player` return stays when that player is null. Immediately after `currentPlayer` is non-null, and before action normalization, validation, `ExecuteAction`, pot changes, or `CurrentActorIndex` changes:

```csharp
if (IsStale(command.BoundSeat, currentPlayer.SeatPosition))
	return new ProcessBettingActionError
	{
		Message = "It is not your turn.",
		Code = ProcessBettingActionErrorCode.NotPlayerTurn
	};
```

Handlers that need this check:

- `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`
- `FiveCardDraw` `ProcessBettingActionCommandHandler.cs`
- `TwosJacksManWithTheAxe` `ProcessBettingActionCommandHandler.cs`
- `SevenCardStud` `ProcessBettingActionCommandHandler.cs`
- `GoodBadUgly` `ProcessBettingActionCommandHandler.cs`
- `Baseball` `ProcessBettingActionCommandHandler.cs`
- `FollowTheQueen` `ProcessBettingActionCommandHandler.cs`
- `HoldTheBaseball` `ProcessBettingActionCommandHandler.cs`

`PairPressure` and `Tollbooth` only copy `BoundSeat` into the Seven Card Stud command. The stud handler performs the check.

Each of those error enums already has `NotPlayerTurn`. The matching endpoint already maps it to 409. Do not add a new code.

Omaha, Nebraska, Red River, South Dakota, Irish Hold'em, Crazy Pineapple, Phil's Mom, Bob Barker, and Klondike send the Hold'em betting command. Their check is the Hold'em handler. Razz sends the Seven Card Stud command. Its check is the stud handler.

### Draw handlers

In `FiveCardDraw` and `TwosJacksManWithTheAxe` `ProcessDrawCommandHandler`, keep the existing `CurrentDrawPlayerIndex < 0` and missing-player returns. Immediately after `currentDrawPlayer` is non-null, and before card-index checks or any card move:

```csharp
if (IsStale(command.BoundSeat, currentDrawPlayer.SeatPosition))
	return new ProcessDrawError
	{
		Message = "It is not your turn.",
		Code = ProcessDrawErrorCode.NotPlayerTurn
	};
```

`TwosJacksManWithTheAxe` already has `NotPlayerTurn` on `ProcessDrawErrorCode`.

### Irish discard

In `ProcessDiscardCommandHandler`, delete this line:

```csharp
var requestedSeatIndex = command.PlayerSeatIndex ?? game.CurrentDrawPlayerIndex;
```

After the existing game-not-found and not-in-discard-phase returns, and before discard-count or card checks, if `command.PlayerSeatIndex` is null or is not `game.CurrentDrawPlayerIndex`, return `ProcessDiscardErrorCode.NotPlayerTurn` with message `It is not your turn.` Do not add a second check against `BoundSeat`. The endpoint already sets `PlayerSeatIndex` from the seated player. A second discard from that same seat fails because the first discard has moved `CurrentDrawPlayerIndex`.

### Commands that already name a seat

Do not add a handler seat check to these. Do add `IBoundToActorSeat` and the property so the behavior can stamp them.

- `FoldDuringDrawCommandHandler` already returns when `command.PlayerSeatIndex != game.CurrentDrawPlayerIndex`. Irish, Crazy Pineapple, and Phil's Mom timers already pass `context.PlayerSeatIndex`.
- `DrawCardsCommandHandler` already returns when the command `PlayerId` is not the current draw player. `KingsAndLowsFlowHandler.SendDrawActionAsync` already loads the player for `context.PlayerSeatIndex` and puts that `PlayerId` on the command.
- `DeckDrawCommandHandler` does not choose a player from `CurrentActorIndex`. Leave its player selection as it is.

## Timer commands

Set `BoundSeat` from `context.PlayerSeatIndex` on the command object. That field is the seat the timer was started for. Do not read `CurrentActorIndex` or `CurrentDrawPlayerIndex` again while building the command.

Set it in `BaseGameFlowHandler.SendBettingActionAsync` and `BaseGameFlowHandler.SendDrawActionAsync`, and in every `SendBettingActionAsync` override:

- `HoldEmFlowHandler`, `OmahaFlowHandler`, `NebraskaFlowHandler`, `RedRiverFlowHandler`, `SouthDakotaFlowHandler`
- `IrishHoldEmFlowHandler`, `CrazyPineappleFlowHandler`, `PhilsMomFlowHandler`
- `BobBarkerFlowHandler`, `KlondikeFlowHandler`, `HoldTheBaseballFlowHandler`
- `SevenCardStudFlowHandler`, `FollowTheQueenFlowHandler`, `GoodBadUglyFlowHandler`, `BaseballFlowHandler`
- `PairPressureFlowHandler`, `TollboothFlowHandler`

`KingsAndLowsFlowHandler.SendDrawActionAsync` stays on `DrawCardsCommand` and its existing `PlayerId`. Do not set `BoundSeat` there and do not add a second check.

Do not change the early returns in `AutoActionService`, `PerformAutoBettingActionAsync`, or `PerformAutoDrawActionAsync`. Those returns skip a timer whose seat is already wrong. They are not the write guard. The handler check is the write guard.

## Tests

Unit tests call `Handle` directly. The behavior does not run, so `BoundSeat` stays null unless the test sets it. After this change a null seat is a refusal once the handler has found the current player. Set `BoundSeat` on every direct `Handle` call that is supposed to get past that line, including calls that then expect a later validation error such as an illegal raise or a bad card index. Set it to the seat the seed stored in `CurrentActorIndex` or `CurrentDrawPlayerIndex`.

Leave `BoundSeat` unset on calls that return before that line: game not found, wrong phase, no betting round, and current player missing. Those assertions stay on the codes they already expect.

`GameAuthorizationBehaviorTests` is the class in `src/Tests/CardGames.IntegrationTests/Api/TrustedIdentityAuthTests.cs`. It is not a separate file.

- `Betting_WhenAuthenticatedPlayerIsCurrentActor_Continues`: the `next` delegate must observe `request.BoundSeat == 0` before it returns `"ok"`.
- `Betting_WithoutHttpContext_AllowsTimerAutoAction`: construct the command with `{ BoundSeat = 1 }`. The result stays `"timer"`. `BoundSeat` is still 1 after `Handle` returns.
- `Betting_WhenAuthenticatedPlayerIsNotCurrentActor_ReturnsNotPlayerTurn`: still throws `GameAuthorizationException` with code `NotPlayerTurn`, and `next` still does not run.

Add these facts to `ProcessBettingActionCommandHandlerTests` for Hold'em. Seed the worked hand. Alice is seat 0 and the current actor.

1. `Handle_WhenBoundSeatMatchesCurrentActor_FoldsThatPlayer`. Command `{ BoundSeat = 0 }`. Alice is folded, `CurrentActorIndex` is 1, one action row exists.
2. `Handle_WhenBoundSeatIsNull_ReturnsNotPlayerTurn`. A current player exists. Result code is `NotPlayerTurn`, message is `It is not your turn.`, Bob is not folded, `CurrentActorIndex` is unchanged, and no action row is added.
3. `Handle_WhenBoundSeatIsThePreviousActor_ReturnsNotPlayerTurn`. First call uses `{ BoundSeat = 0 }` and commits. Second call uses `{ BoundSeat = 0 }` again. The second result is `NotPlayerTurn`. Bob is not folded. `CurrentActorIndex` is 1. One action row exists.

Add the same three facts to the Seven Card Stud betting handler tests, using that handler's seed. The stud handler is the check for Razz, Pair Pressure, and Tollbooth.

Add one fact to the Five Card Draw `ProcessDrawCommandHandler` tests: after a draw committed for the current draw seat, a second `Handle` with that same `BoundSeat` returns `NotPlayerTurn` and does not replace the next player's cards.

Add one fact to `ProcessDiscardCommandHandlerTests`: `Handle` with `PlayerSeatIndex` omitted, on a game already in the discard phase, returns `NotPlayerTurn` and discards nothing. `Handle_WhenRequestedSeatIsMissing_ReturnsNotPlayerTurn` stays on seat 99.

Update existing discard calls that omit `PlayerSeatIndex` and expect an error or a success that happens after the phase check. Pass the seeded `CurrentDrawPlayerIndex` (0 in `SeedGameAsync`). Calls that expect `GameNotFound` or `NotInDiscardPhase` can keep omitting it, because those returns stay before the seat check. `IrishHoldEmHandLifecycleTests` already has a `nonCurrentSeat` discard that must stay `NotPlayerTurn`.

Integration tests inherit `IntegrationTestBase` and call `Mediator.Send`. `HttpContext` is null there, so the behavior does not stamp a seat. Every `Mediator.Send` of a betting or draw command that should act must set `BoundSeat` to the seat that is current at send time. In a loop, read the fresh `CurrentActorIndex` or `CurrentDrawPlayerIndex` on each iteration and put that value on that iteration's command. A discard `Mediator.Send` must pass that draw seat as `PlayerSeatIndex`.

Search the test projects for `new ProcessBettingActionCommand`, `new ProcessDrawCommand`, and `new ProcessDiscardCommand`. Apply the rules above. Do not weaken `IsStale` so that a null seat still acts.

## Do not change

- `ExecuteAction` chip amounts, pot updates, stack caps, or `IsTimeout`
- `HandSettlementService`, wallet methods, and every showdown handler
- `ChooseCard`, `SelectShowcase`, and other commands that are not in the command list above
- Row-version tokens, and any `catch (DbUpdateConcurrencyException)`
- Which command type each flow handler sends. The base draw method keeps sending the Five Card Draw draw command
- The early seat returns in `AutoActionService` and `BaseGameFlowHandler`

## Done when

1. The worked hand's second fold, whether from Alice's client or from the timer started for seat 0, returns `It is not your turn.` Bob is unfolded, `CurrentActorIndex` is 1, and one action row exists.
2. A timer command built with `BoundSeat = context.PlayerSeatIndex` still folds that seat when it is still the current actor.
3. A direct `Handle` with `BoundSeat` null, while a current player exists, returns `NotPlayerTurn` and writes nothing.
4. `dotnet test src/Tests/CardGames.Poker.Tests --filter "FullyQualifiedName~ProcessBettingAction|FullyQualifiedName~ProcessDraw|FullyQualifiedName~ProcessDiscard|FullyQualifiedName~FoldDuringDraw|FullyQualifiedName~DeckDraw|FullyQualifiedName~DrawCards"` passes.
5. `dotnet test src/Tests/CardGames.IntegrationTests --filter "FullyQualifiedName~GameAuthorizationBehaviorTests|FullyQualifiedName~IrishHoldEmHandLifecycleTests|FullyQualifiedName~TrustedIdentityAuthTests"` passes.
