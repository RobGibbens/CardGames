using System;
using System.Collections.Generic;
using CardGames.Core.French.Cards;

namespace CardGames.Poker.Hands.StudHands;

public class FiveCardStudHand : StudHand
{
	public FiveCardStudHand(
		Card holeCard,
		IReadOnlyCollection<Card> openCards)
		: base([holeCard], openCards, [])
	{
		if (openCards.Count > 4) throw new ArgumentException("A five card Stud hand has at most four open cards");
	}
}