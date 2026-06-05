using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.Strength;

namespace CardGames.Poker.Hands.CommunityCardHands;

public sealed class HoldemHand : CommunityCardsHand
{
	public HoldemHand(IReadOnlyCollection<Card> holeCards, IReadOnlyCollection<Card> communityCards)
		: base(0, 2, holeCards, communityCards, HandTypeStrengthRanking.Classic)
	{
	}
}