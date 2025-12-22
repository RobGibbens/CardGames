using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.SevenCardStud;

/// <summary>
/// Represents a player in a Seven Card Stud game with their cards and betting state.
/// </summary>
public class SevenCardStudGamePlayer
{
	public PokerPlayer Player { get; }
	public List<Card> HoleCards { get; private set; } = new List<Card>();
	public List<Card> BoardCards { get; private set; } = new List<Card>();

	public IEnumerable<Card> AllCards => HoleCards.Concat(BoardCards);

	public SevenCardStudGamePlayer(PokerPlayer player)
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

	public void ResetHand()
	{
		HoleCards.Clear();
		BoardCards.Clear();
	}
}