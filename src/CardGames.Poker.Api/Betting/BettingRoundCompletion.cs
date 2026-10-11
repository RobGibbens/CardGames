using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Betting;

/// <summary>
///     Decides when a betting round is over. Posting a blind is not a voluntary action;
///     the big blind must still check, raise, or fold when the pot is limped.
/// </summary>
internal static class BettingRoundCompletion
{
	public static HashSet<Guid> ActedPlayerIds(BettingRound bettingRound, Guid currentPlayerId)
	{
		var actedPlayerIds = bettingRound.Actions
			.Select(action => action.GamePlayerId)
			.ToHashSet();
		actedPlayerIds.Add(currentPlayerId);
		return actedPlayerIds;
	}

	public static bool IsRoundComplete(
		List<GamePlayer> activePlayers,
		BettingRound bettingRound,
		int nextPlayerIndex,
		IReadOnlySet<Guid> actedPlayerIds)
	{
		var playersInHand = activePlayers.Count(player => !player.HasFolded);
		if (playersInHand <= 1) return true;

		var playersWhoCanAct = activePlayers
			.Where(player => !player.HasFolded && !player.IsAllIn)
			.ToList();
		if (playersWhoCanAct.Count == 0) return true;

		var allMatched = playersWhoCanAct.All(player => player.CurrentBet == bettingRound.CurrentBet);
		if (!allMatched) return false;

		if (bettingRound.LastAggressorIndex >= 0 && nextPlayerIndex == bettingRound.LastAggressorIndex)
			return true;

		return playersWhoCanAct.All(player => actedPlayerIds.Contains(player.Id));
	}
}
