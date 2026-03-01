using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Poker.Games.GoodBadUgly;

/// <summary>
/// Result of a Good, Bad, and Ugly showdown.
/// </summary>
public class GoodBadUglyShowdownResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public Dictionary<string, int> Payouts { get; init; }
	public Dictionary<string, (StudHand hand, IReadOnlyCollection<Card> cards)> PlayerHands { get; init; }
	public bool WonByFold { get; init; }

	/// <summary>
	/// The three table cards: The Good, The Bad, The Ugly.
	/// </summary>
	public IReadOnlyList<Card> TableCards { get; init; }

	/// <summary>
	/// The rank (value) that was wild (from "The Good" card). Null if not yet revealed.
	/// </summary>
	public int? WildRank { get; init; }

	/// <summary>
	/// The rank that caused forced discards (from "The Bad" card). Null if not yet revealed.
	/// </summary>
	public int? DiscardRank { get; init; }

	/// <summary>
	/// The rank that eliminated players (from "The Ugly" card). Null if not yet revealed.
	/// </summary>
	public int? EliminationRank { get; init; }

	/// <summary>
	/// Names of players eliminated by "The Ugly".
	/// </summary>
	public IReadOnlyList<string> EliminatedPlayers { get; init; }

	/// <summary>
	/// True when all remaining players were eliminated by "The Ugly" and the pot was split among them.
	/// </summary>
	public bool AllRemainingPlayersEliminatedByUgly { get; init; }
}
