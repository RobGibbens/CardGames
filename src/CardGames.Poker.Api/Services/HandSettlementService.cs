using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Settles per-hand results to the cashier ledger after each showdown.
/// </summary>
public interface IHandSettlementService
{
	/// <summary>
	/// Records per-player net results (win/loss) to the cashier ledger.
	/// </summary>
	/// <param name="game">The game entity with GamePlayers loaded.</param>
	/// <param name="payouts">Dictionary of player name → amount won from pots.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SettleHandAsync(Game game, Dictionary<string, int> payouts, CancellationToken cancellationToken);
}

/// <summary>
/// Default implementation that iterates over active game players, calculates net delta,
/// and calls <see cref="IPlayerChipWalletService.RecordHandSettlementAsync"/> for each.
/// </summary>
public sealed class HandSettlementService(IPlayerChipWalletService walletService) : IHandSettlementService
{
	public async Task SettleHandAsync(Game game, Dictionary<string, int> payouts, CancellationToken cancellationToken)
	{
		// Settle every player who participated in the hand (contributed or won)
		var participants = game.GamePlayers
			.Where(gp => gp.Status == GamePlayerStatus.Active || gp.IsAllIn)
			.ToList();

		foreach (var gp in participants)
		{
			var amountWon = payouts.GetValueOrDefault(gp.Player.Name, 0);
			var netDelta = amountWon - gp.TotalContributedThisHand;

			if (netDelta == 0) continue;

			await walletService.RecordHandSettlementAsync(
				gp.PlayerId,
				netDelta,
				game.Id,
				game.CurrentHandNumber,
				null,
				cancellationToken);
		}
	}
}
