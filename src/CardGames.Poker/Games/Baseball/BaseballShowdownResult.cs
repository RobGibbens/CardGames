using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Poker.Games.Baseball;

/// <summary>
/// Result of a Baseball showdown.
/// </summary>
public class BaseballShowdownResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public Dictionary<string, int> Payouts { get; init; }
	public Dictionary<string, (BaseballHand hand, IReadOnlyCollection<Card> cards)> PlayerHands { get; init; }
	public bool WonByFold { get; init; }
}