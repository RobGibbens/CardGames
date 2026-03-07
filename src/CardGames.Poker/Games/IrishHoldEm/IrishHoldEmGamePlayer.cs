using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.IrishHoldEm;

/// <summary>
/// Represents a player in an Irish Hold 'Em game with their cards, betting state, and discard tracking.
/// </summary>
public class IrishHoldEmGamePlayer
{
	public PokerPlayer Player { get; }
	public List<Card> HoleCards { get; private set; } = new List<Card>();
	public bool HasDiscarded { get; private set; }

	/// <summary>
	/// Convenience property — delegates to Player.HasFolded.
	/// </summary>
	public bool HasFolded => Player.HasFolded;

	public IrishHoldEmGamePlayer(PokerPlayer player)
	{
		Player = player;
	}

	public void AddHoleCard(Card card)
	{
		HoleCards.Add(card);
	}

	/// <summary>
	/// Discards exactly 2 cards by index from the player's hole cards.
	/// </summary>
	/// <param name="indices">Exactly 2 zero-based indices of cards to discard.</param>
	/// <exception cref="InvalidOperationException">Thrown when player has already discarded.</exception>
	/// <exception cref="ArgumentException">Thrown when not exactly 2 indices are provided or indices are out of range.</exception>
	public void DiscardCards(List<int> indices)
	{
		if (HasDiscarded)
			throw new InvalidOperationException("Player has already discarded.");

		if (indices.Count != 2)
			throw new ArgumentException("Must discard exactly 2 cards.", nameof(indices));

		if (indices.Any(i => i < 0 || i >= HoleCards.Count))
			throw new ArgumentException("Card index out of range.", nameof(indices));

		// Remove in descending order to preserve indices
		foreach (var index in indices.OrderByDescending(i => i))
		{
			HoleCards.RemoveAt(index);
		}

		HasDiscarded = true;
	}

	public void ResetHand()
	{
		HoleCards.Clear();
		HasDiscarded = false;
	}
}
