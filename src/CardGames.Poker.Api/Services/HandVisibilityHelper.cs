using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services;

/// <summary>
///     Decides which cards stay public when a hand ends without a contested showdown.
/// </summary>
internal static class HandVisibilityHelper
{
	/// <summary>
	///     True when exactly one player remains in the hand and the pot is awarded
	///     without a multi-player showdown.
	/// </summary>
	internal static bool IsWinByFoldHand(
		Game game,
		IReadOnlyList<GamePlayer> gamePlayers,
		IReadOnlyList<Pot> currentHandPots)
	{
		var playersInHand = gamePlayers
			.Count(gp => !gp.HasFolded
			             && (gp.Status == GamePlayerStatus.Active || gp.IsAllIn));

		if (playersInHand != 1)
			return false;

		if (currentHandPots.Any(p => p.IsAwarded
		                             && string.Equals(p.WinReason, "All others folded",
			                             StringComparison.OrdinalIgnoreCase)))
			return true;

		// Before PerformShowdown: betting handlers set Showdown on fold-win; the award may not exist yet.
		if (string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase)
		    || string.Equals(game.CurrentPhase, "Complete", StringComparison.OrdinalIgnoreCase))
			return gamePlayers.Any(gp => gp.HasFolded);

		return false;
	}
}
