using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Services;

public sealed class PlayerChipWalletService(CardsDbContext context) : IPlayerChipWalletService
{
	public async Task<int> GetBalanceAsync(Guid playerId, CancellationToken cancellationToken)
	{
		var account = await context.PlayerChipAccounts
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);
		return account?.Balance ?? 0;
	}

	public async Task<WalletDebitResult> TryDebitForBuyInAsync(
		Guid playerId,
		int amount,
		Guid gameId,
		string? actorUserId,
		CancellationToken cancellationToken)
	{
		if (amount <= 0)
		{
			return WalletDebitResult.Failure("Buy-in amount must be greater than 0.");
		}

		var now = DateTimeOffset.UtcNow;
		var account = await GetOrCreateAccountAsync(playerId, now, cancellationToken);

		if (account.Balance < amount)
		{
			return WalletDebitResult.Failure(
				$"Insufficient chips in your account. Balance: {account.Balance:N0}, requested: {amount:N0}.");
		}

		// Exposure-limit model: validate only, do NOT debit. Write audit-only BringIn entry.
		context.PlayerChipLedgerEntries.Add(new PlayerChipLedgerEntry
		{
			Id = Guid.CreateVersion7(),
			PlayerId = playerId,
			Type = PlayerChipLedgerEntryType.BringIn,
			AmountDelta = 0,
			BalanceAfter = account.Balance,
			OccurredAtUtc = now,
			ReferenceType = "Game",
			ReferenceId = gameId,
			Reason = $"Table bring-in (exposure limit: {amount:N0})",
			ActorUserId = actorUserId
		});

		return WalletDebitResult.Success(account.Balance);
	}

	public async Task<int?> CreditForCashOutAsync(
		Guid playerId,
		int amount,
		Guid gameId,
		string? actorUserId,
		CancellationToken cancellationToken)
	{
		if (amount <= 0)
		{
			return null;
		}

		var now = DateTimeOffset.UtcNow;
		var account = await GetOrCreateAccountAsync(playerId, now, cancellationToken);

		// Exposure-limit model: results already settled per-hand. Write audit-only CashOut entry.
		context.PlayerChipLedgerEntries.Add(new PlayerChipLedgerEntry
		{
			Id = Guid.CreateVersion7(),
			PlayerId = playerId,
			Type = PlayerChipLedgerEntryType.CashOut,
			AmountDelta = 0,
			BalanceAfter = account.Balance,
			OccurredAtUtc = now,
			ReferenceType = "Game",
			ReferenceId = gameId,
			Reason = $"Table cash-out (in-game balance: {amount:N0})",
			ActorUserId = actorUserId
		});

		return account.Balance;
	}

	public async Task RecordHandSettlementAsync(
		Guid playerId,
		int netDelta,
		Guid gameId,
		int handNumber,
		string? actorUserId,
		CancellationToken cancellationToken)
	{
		if (netDelta == 0) return; // No-op for break-even hands

		// Idempotency check: skip if settlement already recorded for this (player, game, hand)
		var alreadySettled = await context.PlayerChipLedgerEntries
			.AnyAsync(e => e.PlayerId == playerId &&
						   e.ReferenceId == gameId &&
						   e.HandNumber == handNumber &&
						   e.Type == PlayerChipLedgerEntryType.HandSettlement,
				cancellationToken);

		if (alreadySettled) return;

		var now = DateTimeOffset.UtcNow;
		var account = await GetOrCreateAccountAsync(playerId, now, cancellationToken);

		account.Balance += netDelta;
		account.UpdatedAtUtc = now;

		context.PlayerChipLedgerEntries.Add(new PlayerChipLedgerEntry
		{
			Id = Guid.CreateVersion7(),
			PlayerId = playerId,
			Type = PlayerChipLedgerEntryType.HandSettlement,
			AmountDelta = netDelta,
			BalanceAfter = account.Balance,
			OccurredAtUtc = now,
			ReferenceType = "Game",
			ReferenceId = gameId,
			HandNumber = handNumber,
			Reason = netDelta > 0 ? "Hand win" : "Hand loss",
			ActorUserId = actorUserId
		});
	}

	private async Task<PlayerChipAccount> GetOrCreateAccountAsync(
		Guid playerId,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var account = await context.PlayerChipAccounts
			.FirstOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

		if (account is not null)
		{
			return account;
		}

		account = new PlayerChipAccount
		{
			PlayerId = playerId,
			Balance = 0,
			CreatedAtUtc = now,
			UpdatedAtUtc = now
		};

		context.PlayerChipAccounts.Add(account);
		return account;
	}
}
