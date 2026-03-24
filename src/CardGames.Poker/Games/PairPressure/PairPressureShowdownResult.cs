using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Poker.Games.PairPressure;

/// <summary>
/// Result of a Pair Pressure showdown.
/// </summary>
public class PairPressureShowdownResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public Dictionary<string, int> Payouts { get; init; }
	public Dictionary<string, (PairPressureHand hand, IReadOnlyCollection<Card> cards)> PlayerHands { get; init; }
	public bool WonByFold { get; init; }
}