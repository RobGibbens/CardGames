using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Games.Omaha;

/// <summary>
///     Represents a player in an Omaha game with their cards and betting state.
/// </summary>
public class OmahaGamePlayer
{
	public OmahaGamePlayer(PokerPlayer player)
	{
		Player = player;
	}

	public PokerPlayer Player { get; }
	public List<Card> HoleCards { get; } = [];

	public void AddHoleCard(Card card)
	{
		HoleCards.Add(card);
	}

	public void ResetHand()
	{
		HoleCards.Clear();
	}
}