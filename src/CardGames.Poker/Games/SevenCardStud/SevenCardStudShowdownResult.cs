using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Poker.Games.SevenCardStud;

/// <summary>
/// Result of a Seven Card Stud showdown.
/// </summary>
public class SevenCardStudShowdownResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public Dictionary<string, int> Payouts { get; init; }
	public Dictionary<string, (SevenCardStudHand hand, IReadOnlyCollection<Card> cards)> PlayerHands { get; init; }
	public bool WonByFold { get; init; }
}