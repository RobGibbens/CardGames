using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.Omaha;

/// <summary>
/// Represents a player in an Omaha game with their cards and betting state.
/// </summary>
public class OmahaGamePlayer
{
	public PokerPlayer Player { get; }
	public List<Card> HoleCards { get; private set; } = new List<Card>();

	public OmahaGamePlayer(PokerPlayer player)
	{
		Player = player;
	}

	public void AddHoleCard(Card card)
	{
		HoleCards.Add(card);
	}

	public void ResetHand()
	{
		HoleCards.Clear();
	}
}