using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Games.IrishHoldEm;

/// <summary>
/// Result of an Irish Hold 'Em showdown.
/// Uses HoldemHand since players have 2 hole cards post-discard.
/// </summary>
public class IrishHoldEmShowdownResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public Dictionary<string, int> Payouts { get; init; }
	public Dictionary<string, (HoldemHand hand, IReadOnlyCollection<Card> holeCards, IReadOnlyCollection<Card> communityCards)> PlayerHands { get; init; }
	public bool WonByFold { get; init; }
}
