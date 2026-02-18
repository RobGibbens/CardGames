using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services;

public readonly record struct WalletDebitResult(bool Succeeded, int? BalanceAfter, string? ErrorMessage)
{
	public static WalletDebitResult Success(int balanceAfter) => new(true, balanceAfter, null);

	public static WalletDebitResult Failure(string message) => new(false, null, message);
}

public interface IPlayerChipWalletService
{
	Task<WalletDebitResult> TryDebitForBuyInAsync(
		Guid playerId,
		int amount,
		Guid gameId,
		string? actorUserId,
		CancellationToken cancellationToken);

	Task<int?> CreditForCashOutAsync(
		Guid playerId,
		int amount,
		Guid gameId,
		string? actorUserId,
		CancellationToken cancellationToken);
}
