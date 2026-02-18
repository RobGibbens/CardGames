using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Services;

public sealed class PlayerChipWalletService(CardsDbContext context) : IPlayerChipWalletService
{
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

		account.Balance -= amount;
		account.UpdatedAtUtc = now;

		context.PlayerChipLedgerEntries.Add(new PlayerChipLedgerEntry
		{
			Id = Guid.CreateVersion7(),
			PlayerId = playerId,
			Type = PlayerChipLedgerEntryType.BuyIn,
			AmountDelta = -amount,
			BalanceAfter = account.Balance,
			OccurredAtUtc = now,
			ReferenceType = "Game",
			ReferenceId = gameId,
			Reason = "Table buy-in",
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

		account.Balance += amount;
		account.UpdatedAtUtc = now;

		context.PlayerChipLedgerEntries.Add(new PlayerChipLedgerEntry
		{
			Id = Guid.CreateVersion7(),
			PlayerId = playerId,
			Type = PlayerChipLedgerEntryType.CashOut,
			AmountDelta = amount,
			BalanceAfter = account.Balance,
			OccurredAtUtc = now,
			ReferenceType = "Game",
			ReferenceId = gameId,
			Reason = "Table cash-out",
			ActorUserId = actorUserId
		});

		return account.Balance;
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
