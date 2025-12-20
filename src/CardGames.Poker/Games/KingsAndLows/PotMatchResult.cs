using System.Collections.Generic;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.KingsAndLows;

/// <summary>
/// Result of pot matching.
/// </summary>
public class PotMatchResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public IReadOnlyList<BettingAction> MatchActions { get; init; }
	public int NewPotAmount { get; init; }
}