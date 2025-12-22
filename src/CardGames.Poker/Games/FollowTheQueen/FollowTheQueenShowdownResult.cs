using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Poker.Games.FollowTheQueen;

/// <summary>
/// Result of a Follow the Queen showdown.
/// </summary>
public class FollowTheQueenShowdownResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public Dictionary<string, int> Payouts { get; init; }
	public Dictionary<string, (FollowTheQueenHand hand, IReadOnlyCollection<Card> cards)> PlayerHands { get; init; }
	public bool WonByFold { get; init; }
}