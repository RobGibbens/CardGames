using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.TwosJacksManWithTheAxe;

/// <summary>
/// Represents a player in a Five Card Draw game with their cards and betting state.
/// </summary>
public class TwosJacksManWithTheAxeGamePlayer
{
	/// <summary>
	/// Gets the underlying poker player containing identity and chip stack information.
	/// This provides access to the player's name, chip count, and betting state
	/// which persists across multiple hands in a game session.
	/// </summary>
	public PokerPlayer Player { get; }

	/// <summary>
	/// Gets the player's current five-card hand.
	/// The hand is populated during the deal phase and may be modified during the draw phase
	/// when the player discards and receives replacement cards.
	/// </summary>
	public List<Card> Hand { get; private set; } = [];

	public TwosJacksManWithTheAxeGamePlayer(PokerPlayer player)
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

	public void ResetHand()
	{
		Hand.Clear();
	}
}