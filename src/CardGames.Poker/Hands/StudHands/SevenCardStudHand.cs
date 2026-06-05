using System.Collections.Generic;
using CardGames.Core.French.Cards;

namespace CardGames.Poker.Hands.StudHands;

public class SevenCardStudHand : StudHand
{
	public SevenCardStudHand(
		IReadOnlyCollection<Card> holeCards,
		IReadOnlyCollection<Card> openCards,
		Card downCard) : base(holeCards, openCards, [downCard])
	{
	}
}