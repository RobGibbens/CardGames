using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Games.Omaha;

/// <summary>
/// Result of an Omaha showdown.
/// </summary>
public class OmahaShowdownResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public Dictionary<string, int> Payouts { get; init; }
	public Dictionary<string, (OmahaHand hand, IReadOnlyCollection<Card> holeCards, IReadOnlyCollection<Card> communityCards)> PlayerHands { get; init; }
	public bool WonByFold { get; init; }
}