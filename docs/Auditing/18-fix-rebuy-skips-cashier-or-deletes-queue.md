# Fix: immediate rebuy skips the cashier, and a queued rebuy can be deleted

Implement this document only. The cashier is an exposure check. `TryDebitForBuyInAsync` allows the bring-in when `PlayerChipAccount.Balance` is at least the amount, then writes a `BringIn` ledger row with `AmountDelta` 0. It does not change `Balance`. `RecordHandSettlementAsync` is the only method that changes `Balance`.

## Defect

`AddChipsCommandHandler` never calls the wallet.

When `applyImmediately` is true, it adds `command.Amount` to `ChipStack` and saves. A player whose balance is 100 can put 250 on the table. No `BringIn` row is written. `BringInAmount` stays unchanged. Kings and Lows, rebuy grace, chip-check pause, `BetweenHands`, and `WaitingForPlayers` all take this branch.

When `applyImmediately` is false, it adds the amount to `PendingChipsToAdd` with the same missing check. `CreateCompleteGameSetupAsync` leaves the phase at `WaitingToStart`, and that phase is not in the immediate list, so a normal in-hand add is queued.

`StartNextHandAsync` later reads the cashier. When `Balance < PendingChipsToAdd` it sets `PendingChipsToAdd = 0` and continues. The queued rebuy is gone. The stack does not increase. Nothing is written to the ledger.

Every `StartHand` handler has its own copy of the apply loop. Those copies add `PendingChipsToAdd` to `ChipStack` and set `PendingChipsToAdd` to 0 with no cashier call. A host starting the hand therefore both skips the check and clears the queue.

## Worked hand

Alice’s cashier balance is 100. Her table stack is 40. `BringInAmount` is 40. `PendingChipsToAdd` is 0. Five Card Draw. `Ante` is 10. `TournamentBuyIn` is null.

### Immediate rebuy above the balance

Phase is `WaitingForPlayers`. Alice adds 250.

Today her stack becomes 290. Balance stays 100. No `BringIn` row exists.

After this fix the handler returns `AddChipsErrorCode.InsufficientAccountChips` and the message from `TryDebitForBuyInAsync`: `Insufficient chips in your account. Balance: 100, requested: 250.` The stack stays 40. `PendingChipsToAdd` stays 0. `BringInAmount` stays 40. Balance stays 100. No `BringIn` row is added.

### Immediate rebuy inside the balance

The same seat, balance 500, stack 40, `BringInAmount` 40. She adds 200 while the phase is `WaitingForPlayers`.

Her stack becomes 240. `BringInAmount` becomes 240. `PendingChipsToAdd` stays 0. Balance stays 500. One new `PlayerChipLedgerEntry` exists: `Type` `BringIn`, `AmountDelta` 0, `BalanceAfter` 500, `ReferenceType` `"Game"`, `ReferenceId` the game id, `Reason` `Table bring-in (exposure limit: 200)`.

### Queued rebuy, then a short balance

Phase is `PreFlop`, so the add is queued. Balance is 100. She adds 80.

`PendingChipsToAdd` becomes 80. The stack stays 40. Balance stays 100. No `BringIn` row exists yet.

A settlement then leaves her balance at 30 before the next hand. `PendingChipsToAdd` is still 80.

Today `StartNextHandAsync` sets `PendingChipsToAdd` to 0. The stack stays 40.

After this fix the 80 stays on `PendingChipsToAdd`. The stack stays 40. `BringInAmount` stays 40. `IsSittingOut` and `Status` stay as they were. No `BringIn` row is written.

When her balance is 100 again, the next apply calls `TryDebitForBuyInAsync` for 80. Balance stays 100. The stack becomes 120. `BringInAmount` becomes 120. `PendingChipsToAdd` becomes 0. One `BringIn` row records amount 80 with `AmountDelta` 0.

### A second queue that no longer fits

`PendingChipsToAdd` is 80. Balance is 100. She queues another 30. The total would be 110.

The handler returns `InsufficientAccountChips` with message `Insufficient chips in your account. Balance: 100, requested: 110.` `PendingChipsToAdd` stays 80. The stack does not change. No `BringIn` row is written.

## Add chips

Add `IPlayerChipWalletService playerChipWalletService` to the `AddChipsCommandHandler` constructor. The API and `IntegrationTestBase` already register that service on the same scoped `CardsDbContext`.

Add `InsufficientAccountChips` to `AddChipsErrorCode`. Map it in `AddChipsEndpoint` to `Results.Conflict(new { error.Message })`, the same status join uses for `JoinGameErrorCode.InsufficientAccountChips`.

Leave the `applyImmediately` expression as it is. Do not remove an existing `game.UpdatedAt = DateTimeOffset.UtcNow` assignment in the rebuy-grace branch. Do not add that assignment in this change.

After the existing game, player, left-player, and authorization returns, and before either chip update:

Immediate branch, `applyImmediately` is true:

```csharp
var debit = await playerChipWalletService.TryDebitForBuyInAsync(
	gamePlayer.PlayerId,
	command.Amount,
	command.GameId,
	currentUserService.UserId,
	cancellationToken);
if (!debit.Succeeded)
	return new AddChipsError(
		AddChipsErrorCode.InsufficientAccountChips,
		debit.ErrorMessage ?? "Insufficient chips in your account.");
```

Then keep the current stack update: `ChipStack += command.Amount`, `IsSittingOut = false`, and `SittingOut` becomes `Active`. Also add `gamePlayer.BringInAmount += command.Amount`. The `BringIn` row `TryDebitForBuyInAsync` added is saved by the `SaveChanges` already at the bottom of `Handle`.

Queued branch:

```csharp
var balance = await playerChipWalletService.GetBalanceAsync(gamePlayer.PlayerId, cancellationToken);
var requested = gamePlayer.PendingChipsToAdd + command.Amount;
if (balance < requested)
	return new AddChipsError(
		AddChipsErrorCode.InsufficientAccountChips,
		$"Insufficient chips in your account. Balance: {balance:N0}, requested: {requested:N0}.");
```

Then `PendingChipsToAdd += command.Amount`. Do not call `TryDebitForBuyInAsync` on this branch. Do not change `ChipStack` or `BringInAmount`.

A missing account is created by the wallet at `CashierAccountInitializer.StartingChipAmount` (100_000). Existing add-chips tests that do not seed an account still cover their amounts, because 200 is below 100_000.

## One apply method

Add this method in `src/CardGames.Poker.Api/Services/PendingRebuyApplicator.cs`. `CardGames.IntegrationTests` and `CardGames.Poker.Tests` are already `InternalsVisibleTo` friends.

```csharp
internal static class PendingRebuyApplicator
{
	internal static async Task ApplyAsync(
		Game game,
		IPlayerChipWalletService wallet,
		CancellationToken cancellationToken)
	{
		var players = game.GamePlayers
			.Where(gp => gp.Status is GamePlayerStatus.Active or GamePlayerStatus.SittingOut
			             && gp.PendingChipsToAdd > 0)
			.ToList();

		foreach (var player in players)
		{
			var debit = await wallet.TryDebitForBuyInAsync(
				player.PlayerId,
				player.PendingChipsToAdd,
				game.Id,
				null,
				cancellationToken);
			if (!debit.Succeeded)
				continue;

			player.ChipStack += player.PendingChipsToAdd;
			player.BringInAmount += player.PendingChipsToAdd;
			player.PendingChipsToAdd = 0;
			player.IsSittingOut = false;
			if (player.Status == GamePlayerStatus.SittingOut)
				player.Status = GamePlayerStatus.Active;
		}
	}
}
```

`continue` leaves `PendingChipsToAdd` at the stored value. That is the deletion fix. Do not set it to 0 on a failed debit. Do not apply a partial amount. Do not write a log line that says the rebuy was cancelled.

The wallet and the game must use the same `CardsDbContext`. `TryDebitForBuyInAsync` only adds the ledger row. The caller’s existing `SaveChanges` persists it with the stack update.

Replace the pending-chip loop in `ContinuousPlayBackgroundService.StartNextHandAsync` with `await PendingRebuyApplicator.ApplyAsync(game, playerChipWalletService, cancellationToken)`. Keep the `SaveChanges` that already follows that loop when any of those players had pending chips. Delete the `GetBalanceAsync` check and the assignment `player.PendingChipsToAdd = 0` that runs when the balance is short.

Replace each local pending-chip loop in these `StartHand` handlers with `await PendingRebuyApplicator.ApplyAsync(game, playerChipWalletService, cancellationToken)` at the same point in `Handle`, before the auto-sit-out pass:

- `FiveCardDraw/v1/Commands/StartHand/StartHandCommandHandler.cs`
- `TwosJacksManWithTheAxe/v1/Commands/StartHand/StartHandCommandHandler.cs`
- `SevenCardStud/v1/Commands/StartHand/StartHandCommandHandler.cs`
- `FollowTheQueen/v1/Commands/StartHand/StartHandCommandHandler.cs`
- `Baseball/v1/Commands/StartHand/StartHandCommandHandler.cs`
- `HoldTheBaseball/v1/Commands/StartHand/StartHandCommandHandler.cs`
- `GoodBadUgly` has no `StartHand` apply loop; skip it if there is no `PendingChipsToAdd = 0` there
- `PairPressure/v1/Commands/StartHand/StartHandCommandHandler.cs`
- `Tollbooth/v1/Commands/StartHand/StartHandCommandHandler.cs`
- `HoldEm/v1/Commands/StartHand/StartHandCommandHandler.cs`
- `KingsAndLows/v1/Commands/StartHand/StartHandCommandHandler.cs`
- `Generic/v1/Commands/StartHand/StartHandCommandHandler.cs`

Add `IPlayerChipWalletService playerChipWalletService` as the parameter immediately after `CardsDbContext context` on each of those handlers. Required parameters stay in front of any `GameEventsTelemetry? telemetry = null` parameter.

Update the three `new StartHandCommandHandler(...)` sites to pass `new PlayerChipWalletService(context)` in that same position:

- `src/Tests/CardGames.Poker.Tests/Api/Features/Games/HoldEm/v1/Commands/StartHand/StartHandCommandHandlerTests.cs`
- `src/Tests/CardGames.Poker.Tests/Api/Features/Games/HoldTheBaseball/v1/Commands/StartHand/StartHandCommandHandlerTests.cs`
- `src/Tests/CardGames.Poker.Tests/Api/Features/Games/PairPressure/v1/Commands/StartHand/StartHandCommandHandlerTests.cs`

`Handle_AppliesPendingChips` seeds `PendingChipsToAdd` 500 and no cashier account. The wallet creates a 100_000 balance, the debit succeeds, and the stack assertion stays 600 with pending 0.

## Tests

Seed the account in these tests. A missing account becomes 100_000 and will not show the refusal.

Add these facts to `AddChipsCommandHandlerTests`:

1. `Handle_WaitingForPlayers_BalanceBelowAmount_ReturnsInsufficientAccountChips`. Phase `WaitingForPlayers`. Balance 100. Amount 250. Stack, `BringInAmount`, and `PendingChipsToAdd` are unchanged. Balance stays 100. No `BringIn` row for that player and game.
2. `Handle_WaitingForPlayers_BalanceCoversAmount_WritesBringInAndKeepsBalance`. Balance 500. Amount 200. Stack and `BringInAmount` each increase by 200. Balance stays 500. The new ledger row is `BringIn`, `AmountDelta` 0, `BalanceAfter` 500, `ReferenceId` the game id.
3. `Handle_PreFlop_QueuesWhenBalanceCoversAmount`. Phase `PreFlop`. Balance 100. Amount 80. `PendingChipsToAdd` is 80. Stack unchanged. No `BringIn` row.
4. `Handle_PreFlop_SecondQueueAboveBalance_LeavesTheFirstQueue`. After fact 3’s state, amount 30. Result code `InsufficientAccountChips`. Message contains `requested: 110`. `PendingChipsToAdd` stays 80.
5. `Handle_KingsAndLows_BalanceBelowAmount_DoesNotApplyImmediately`. Phase `FirstBettingRound`, status `InProgress`, game type Kings and Lows. Balance 100. Amount 200. Result code `InsufficientAccountChips`. Stack unchanged.

Add two facts to `ContinuousPlayBackgroundServiceTests`. Seed a cash game the next-hand query selects (`CurrentPhase` `Complete`, `NextHandStartsAt` one second in the past, status `InProgress`), two active players with stacks above the ante, and Alice’s `PendingChipsToAdd` at 80.

1. `ProcessGamesReadyForNextHandAsync_ShortBalance_LeavesQueuedRebuy`. Alice’s `PlayerChipAccount.Balance` is 30. After the call her `PendingChipsToAdd` is 80, her stack is unchanged, and she has no `BringIn` row.
2. `ProcessGamesReadyForNextHandAsync_CoveredBalance_AppliesQueuedRebuy`. Balance is 100. After the call her `PendingChipsToAdd` is 0, her stack increased by 80, `BringInAmount` increased by 80, balance is still 100, and one `BringIn` row has `AmountDelta` 0.

`ContinuousPlayBackgroundServiceTests` builds `PlayerChipWalletService` on the same in-memory context the service saves. Seed `PlayerChipAccount` on that context before the act.

## Do not change

- `TryDebitForBuyInAsync`, `CreditForCashOutAsync`, and `RecordHandSettlementAsync`. Balance still does not move on a bring-in. A hand loss can still make `Balance` negative. This change does not add a floor.
- `applyImmediately`, including Kings and Lows and rebuy grace.
- Join and rejoin. Rejoin still sets `PendingChipsToAdd = 0` on the previous seat without applying it.
- Showdown and settlement.
- The rebuy-grace expiry query and `ApplyCashRebuyGraceExpiryAsync` from the rebuy-grace plan. This change does not add a game-row touch.

## Done when

1. An immediate add larger than `Balance` returns `InsufficientAccountChips` and leaves the stack, the queue, `BringInAmount`, and `Balance` unchanged, with no `BringIn` row.
2. An immediate add within `Balance` increases the stack and `BringInAmount` by that amount, leaves `Balance` unchanged, and writes one `BringIn` row with `AmountDelta` 0.
3. A queued add within `Balance` increases `PendingChipsToAdd` only. A later queued add whose total exceeds `Balance` is refused and the first queue remains.
4. A next-hand pass with `Balance` below `PendingChipsToAdd` leaves that queue in place and does not add those chips.
5. A next-hand pass with `Balance` at least `PendingChipsToAdd` moves the chips, writes one `BringIn` row, and sets `PendingChipsToAdd` to 0.
6. `dotnet test src/Tests/CardGames.IntegrationTests --filter "FullyQualifiedName~AddChipsCommandHandlerTests|FullyQualifiedName~ContinuousPlayBackgroundServiceTests|FullyQualifiedName~StartHandCommandHandlerIntegrationTests"` passes.
7. `dotnet test src/Tests/CardGames.Poker.Tests --filter "FullyQualifiedName~StartHandCommandHandlerTests"` passes.
