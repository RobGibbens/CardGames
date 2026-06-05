using System.Collections.Generic;
using CardGames.Core.French.Cards;

namespace CardGames.Poker.Hands.DrawHands;

public abstract class FiveCardHand : HandBase
{
	protected FiveCardHand(IReadOnlyCollection<Card> cards)
		: base(cards)
	{
	}
}