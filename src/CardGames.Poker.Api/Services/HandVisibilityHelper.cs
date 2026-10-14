using System.Text.Json;
using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services;

/// <summary>
///     Decides which cards stay public when a hand ends without a contested showdown.
/// </summary>
internal static class HandVisibilityHelper
{
	/// <summary>
	///     True when exactly one player remains in the hand and the pot is awarded
	///     without a multi-player showdown. A Kings and Lows five-card deck hand is a
	///     player-against-the-deck showdown, even before the pot is awarded.
	/// </summary>
	internal static bool IsWinByFoldHand(
		Game game,
		IReadOnlyList<GamePlayer> gamePlayers,
		IReadOnlyList<Pot> currentHandPots,
		bool hasKingsAndLowsDeckHand = false)
	{
		var playersInHand = gamePlayers
			.Count(gp => !gp.HasFolded
			             && (gp.Status == GamePlayerStatus.Active || gp.IsAllIn));

		if (playersInHand != 1)
			return false;

		if (hasKingsAndLowsDeckHand)
			return false;

		if (currentHandPots.Any(p => p.IsAwarded
		                             && (string.Equals(p.WinReason, "All others folded",
			                                 StringComparison.OrdinalIgnoreCase)
		                                 || string.Equals(p.WinReason, "All others dropped",
			                                 StringComparison.OrdinalIgnoreCase))))
			return true;

		// Before PerformShowdown: betting handlers set Showdown on fold-win; the award may not exist yet.
		if (string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase)
		    || string.Equals(game.CurrentPhase, "Complete", StringComparison.OrdinalIgnoreCase))
			return gamePlayers.Any(gp => gp.HasFolded);

		return false;
	}

	/// <summary>
	///     True while an all-in runout is at showdown and no pot for the supplied
	///     current-hand list has been awarded. Face-down cards stay unpublished in that window.
	/// </summary>
	internal static bool IsUnawardedAllInRunout(Game game, IReadOnlyList<Pot> currentHandPots)
	{
		if (!string.Equals(game.CurrentPhase, "Showdown", StringComparison.OrdinalIgnoreCase))
			return false;

		if (string.IsNullOrWhiteSpace(game.GameSettings))
			return false;

		if (currentHandPots.Any(p => p.IsAwarded))
			return false;

		try
		{
			using var settingsDoc = JsonDocument.Parse(game.GameSettings);
			var root = settingsDoc.RootElement;
			if (root.ValueKind != JsonValueKind.Object)
				return false;

			if (!root.TryGetProperty("allInRunout", out var allInRunoutProp)
			    || allInRunoutProp.ValueKind != JsonValueKind.True)
				return false;

			if (root.TryGetProperty("runoutHandNumber", out var handNumberProp)
			    && (handNumberProp.ValueKind != JsonValueKind.Number
			        || !handNumberProp.TryGetInt32(out var runoutHandNumber)
			        || runoutHandNumber != game.CurrentHandNumber))
				return false;

			return true;
		}
		catch (JsonException)
		{
			return false;
		}
	}
}
