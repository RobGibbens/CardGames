using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.KingsAndLows;

/// <summary>
/// Represents a player in a Kings and Lows game with their cards and state.
/// </summary>
public class KingsAndLowsGamePlayer
{
	public PokerPlayer Player { get; }
	public List<Card> Hand { get; private set; } = [];
	public DropOrStayDecision Decision { get; private set; } = DropOrStayDecision.Undecided;
	public bool HasDropped => Decision == DropOrStayDecision.Drop;
	public bool HasStayed => Decision == DropOrStayDecision.Stay;

	public KingsAndLowsGamePlayer(PokerPlayer player)
	{
		Player = player;
	}

	public void SetHand(IEnumerable<Card> cards)
	{
		Hand = cards.ToList();
	}

	public void DiscardAndDraw(IReadOnlyCollection<int> discardIndices, IReadOnlyCollection<Card> newCards)
	{
		// Remove cards at specified indices (in descending order to avoid index shifting)
		foreach (var index in discardIndices.OrderByDescending(i => i))
		{
			if (index >= 0 && index < Hand.Count)
			{
				Hand.RemoveAt(index);
			}
		}

		// Add new cards
		Hand.AddRange(newCards);
	}

	public void ResetForNewHand()
	{
		Hand.Clear();
		Decision = DropOrStayDecision.Undecided;
	}

	public void SetDecision(DropOrStayDecision decision)
	{
		Decision = decision;
	}
}