using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.GoodBadUgly;

/// <summary>
/// Represents a player in a Good, Bad, and Ugly game with their cards and state.
/// Extends the standard stud player with discard and elimination support.
/// </summary>
public class GoodBadUglyGamePlayer
{
	public PokerPlayer Player { get; }
	public List<Card> HoleCards { get; private set; } = new List<Card>();

	/// <summary>
	/// Whether this player has a dead hand due to "The Ugly" card.
	/// Dead-hand players can still bet the final round but cannot win if any non-dead player remains.
	/// </summary>
	public bool IsEliminatedByUgly { get; private set; }

	/// <summary>
	/// Cards that were discarded due to "The Bad" card.
	/// </summary>
	public List<Card> DiscardedCards { get; private set; } = new List<Card>();

	public IEnumerable<Card> AllCards => HoleCards;

	public GoodBadUglyGamePlayer(PokerPlayer player)
	{
		Player = player;
	}

	public void AddHoleCard(Card card)
	{
		HoleCards.Add(card);
	}

	/// <summary>
	/// Removes all cards matching the given rank (value) from the player's hand.
	/// Cards are moved to the DiscardedCards list. Used when "The Bad" card is revealed.
	/// </summary>
	/// <param name="rank">The card rank (value) to discard.</param>
	/// <returns>The cards that were discarded.</returns>
	public List<Card> RemoveMatchingCards(int rank)
	{
		var discarded = new List<Card>();

		var matchingHole = HoleCards.Where(c => c.Value == rank).ToList();
		foreach (var card in matchingHole)
		{
			HoleCards.Remove(card);
			discarded.Add(card);
		}

		DiscardedCards.AddRange(discarded);
		return discarded;
	}

	/// <summary>
	/// Checks if the player has any card matching the given rank.
	/// Used to determine dead-hand status from "The Ugly".
	/// </summary>
	/// <param name="rank">The card rank (value) to check.</param>
	/// <returns>True if the player has a matching card.</returns>
	public bool HasMatchingCard(int rank)
	{
		return HoleCards.Any(c => c.Value == rank);
	}

	/// <summary>
	/// Marks this player as having a dead hand from "The Ugly" card.
	/// Does not fold the player; they remain eligible to bet final round.
	/// </summary>
	public void EliminateByUgly()
	{
		IsEliminatedByUgly = true;
	}

	public void ResetHand()
	{
		HoleCards.Clear();
		DiscardedCards.Clear();
		IsEliminatedByUgly = false;
	}
}
