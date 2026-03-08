using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services;

public readonly record struct WalletDebitResult(bool Succeeded, int? BalanceAfter, string? ErrorMessage)
{
	public static WalletDebitResult Success(int balanceAfter) => new(true, balanceAfter, null);

	public static WalletDebitResult Failure(string message) => new(false, null, message);
}

public interface IPlayerChipWalletService
{
	Task<int> GetBalanceAsync(Guid playerId, CancellationToken cancellationToken);

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

	/// <summary>
	/// Records a per-hand settlement to the cashier ledger. Adjusts the player's account balance by netDelta.
	/// Idempotent: skips if a HandSettlement entry already exists for (PlayerId, GameId, HandNumber).
	/// </summary>
	Task RecordHandSettlementAsync(
		Guid playerId,
		int netDelta,
		Guid gameId,
		int handNumber,
		string? actorUserId,
		CancellationToken cancellationToken);
}
