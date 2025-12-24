using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.HoldEm;

/// <summary>
/// Represents a player in a Hold 'Em game with their cards and betting state.
/// </summary>
public class HoldEmGamePlayer
{
	public PokerPlayer Player { get; }
	public List<Card> HoleCards { get; private set; } = [];

	public HoldEmGamePlayer(PokerPlayer player)
	{
		Player = player;
	}

	public void SetHoleCards(IEnumerable<Card> cards)
	{
		HoleCards = cards.ToList();
	}

	public void ResetHand()
	{
		HoleCards.Clear();
	}
}