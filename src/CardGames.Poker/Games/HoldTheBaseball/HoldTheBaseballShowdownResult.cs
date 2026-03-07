using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Games.HoldTheBaseball;

/// <summary>
/// Result of a Hold the Baseball showdown.
/// </summary>
public class HoldTheBaseballShowdownResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public Dictionary<string, int> Payouts { get; init; }
	public Dictionary<string, (HoldTheBaseballHand hand, IReadOnlyCollection<Card> holeCards)> PlayerHands { get; init; }
	public bool WonByFold { get; init; }
}
