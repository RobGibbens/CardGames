using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.DrawHands;

namespace CardGames.Poker.Games.KingsAndLows;

/// <summary>
/// Result of a Kings and Lows showdown.
/// </summary>
public class KingsAndLowsShowdownResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public Dictionary<string, int> Payouts { get; init; }
	public Dictionary<string, (DrawHand hand, IReadOnlyCollection<Card> cards)> PlayerHands { get; init; }
	public bool IsPlayerVsDeck { get; init; }
	public bool DeckWon { get; init; }
	public bool IsTie { get; init; }
	public IReadOnlyList<string> Winners { get; init; }
	public IReadOnlyList<string> Losers { get; init; }
	public IReadOnlyDictionary<string, int> PotMatchAmounts { get; init; }
	public int PotBeforeShowdown { get; init; }
}