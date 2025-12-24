using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Games.HoldEm;

/// <summary>
/// Result of a Hold 'Em showdown.
/// </summary>
public class HoldEmShowdownResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public Dictionary<string, int> Payouts { get; init; }
	public Dictionary<string, (HoldemHand hand, IReadOnlyCollection<Card> holeCards)> PlayerHands { get; init; }
	public bool WonByFold { get; init; }
}