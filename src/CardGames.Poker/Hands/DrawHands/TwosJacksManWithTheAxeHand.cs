using System;
using System.Collections.Generic;
using CardGames.Core.French.Cards;

namespace CardGames.Poker.Hands.DrawHands;

public abstract class TwosJacksManWithTheAxeHand : HandBase
{
	protected TwosJacksManWithTheAxeHand(IReadOnlyCollection<Card> cards)
		: base(cards)
	{
		if (cards.Count != 5)
		{
			throw new ArgumentException("A five card hand requires exactly 5 cards!");
		}
	}
}