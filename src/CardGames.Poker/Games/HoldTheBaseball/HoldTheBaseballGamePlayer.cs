using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.HoldTheBaseball;

/// <summary>
/// Represents a player in a Hold the Baseball game with their cards and betting state.
/// </summary>
public class HoldTheBaseballGamePlayer
{
	public PokerPlayer Player { get; }
	public List<Card> HoleCards { get; private set; } = [];

	public HoldTheBaseballGamePlayer(PokerPlayer player)
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
