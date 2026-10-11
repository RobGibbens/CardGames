using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services;

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
