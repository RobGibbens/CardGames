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
	public List<Card> BoardCards { get; private set; } = new List<Card>();

	/// <summary>
	/// Whether this player was eliminated by "The Ugly" card.
	/// </summary>
	public bool IsEliminatedByUgly { get; private set; }

	/// <summary>
	/// Cards that were discarded due to "The Bad" card.
	/// </summary>
	public List<Card> DiscardedCards { get; private set; } = new List<Card>();

	public IEnumerable<Card> AllCards => HoleCards.Concat(BoardCards);

	public GoodBadUglyGamePlayer(PokerPlayer player)
	{
		Player = player;
	}

	public void AddHoleCard(Card card)
	{
		HoleCards.Add(card);
	}

	public void AddBoardCard(Card card)
	{
		BoardCards.Add(card);
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

		var matchingBoard = BoardCards.Where(c => c.Value == rank).ToList();
		foreach (var card in matchingBoard)
		{
			BoardCards.Remove(card);
			discarded.Add(card);
		}

		DiscardedCards.AddRange(discarded);
		return discarded;
	}

	/// <summary>
	/// Checks if the player has any face-up (board) card matching the given rank.
	/// Used to determine elimination by "The Ugly".
	/// </summary>
	/// <param name="rank">The card rank (value) to check.</param>
	/// <returns>True if the player has a matching face-up card.</returns>
	public bool HasMatchingBoardCard(int rank)
	{
		return BoardCards.Any(c => c.Value == rank);
	}

	/// <summary>
	/// Marks this player as eliminated by "The Ugly" card and folds them.
	/// </summary>
	public void EliminateByUgly()
	{
		IsEliminatedByUgly = true;
		Player.Fold();
	}

	public void ResetHand()
	{
		HoleCards.Clear();
		BoardCards.Clear();
		DiscardedCards.Clear();
		IsEliminatedByUgly = false;
	}
}
