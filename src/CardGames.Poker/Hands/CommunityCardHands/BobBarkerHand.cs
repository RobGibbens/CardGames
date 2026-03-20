using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.Strength;

namespace CardGames.Poker.Hands.CommunityCardHands;

public sealed class BobBarkerHand : CommunityCardsHand
{
    public BobBarkerHand(IReadOnlyCollection<Card> holeCards, IReadOnlyCollection<Card> communityCards)
        : base(2, 2, holeCards, communityCards, HandTypeStrengthRanking.Classic)
    {
    }
}