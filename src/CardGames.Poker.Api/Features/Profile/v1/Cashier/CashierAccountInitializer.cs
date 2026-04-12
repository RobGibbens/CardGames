using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Profile.v1.Cashier;

internal static class CashierAccountInitializer
{
	public const int StartingChipAmount = 100_000;
	public const string RegistrationReferenceType = "Registration";
	public const string RegistrationReason = "Initial registration chips";

	public static Task<PlayerChipAccount> EnsureRegistrationCreditAsync(
		CardsDbContext context,
		Guid playerId,
		string? actorUserId,
		CancellationToken cancellationToken)
	{
		return EnsureAccountInitializedAsync(
			context,
			playerId,
			StartingChipAmount,
			RegistrationReferenceType,
			RegistrationReason,
			actorUserId,
			cancellationToken);
	}

	public static async Task<PlayerChipAccount> EnsureAccountInitializedAsync(
		CardsDbContext context,
		Guid playerId,
		int openingBalance,
		string referenceType,
		string? reason,
		string? actorUserId,
		CancellationToken cancellationToken)
	{
		var trackedAccount = context.PlayerChipAccounts.Local.FirstOrDefault(x => x.PlayerId == playerId);
		if (trackedAccount is not null)
		{
			return trackedAccount;
		}

		var existingAccount = await context.PlayerChipAccounts
			.FirstOrDefaultAsync(x => x.PlayerId == playerId, cancellationToken);

		if (existingAccount is not null)
		{
			return existingAccount;
		}

		var now = DateTimeOffset.UtcNow;
		var account = new PlayerChipAccount
		{
			PlayerId = playerId,
			Balance = openingBalance,
			CreatedAtUtc = now,
			UpdatedAtUtc = now
		};

		context.PlayerChipAccounts.Add(account);

		if (openingBalance != 0)
		{
			context.PlayerChipLedgerEntries.Add(new PlayerChipLedgerEntry
			{
				Id = Guid.CreateVersion7(),
				PlayerId = playerId,
				Type = PlayerChipLedgerEntryType.Add,
				AmountDelta = openingBalance,
				BalanceAfter = openingBalance,
				OccurredAtUtc = now,
				ReferenceType = referenceType,
				Reason = reason,
				ActorUserId = actorUserId
			});
		}

		return account;
	}
}