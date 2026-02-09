using CardGames.Core.French.Cards;
using System;
using System.Collections.Generic;

namespace CardGames.Poker.Hands.DrawHands;

public abstract class FiveCardHand : HandBase
{
    protected FiveCardHand(IReadOnlyCollection<Card> cards)
        : base(cards)
    {
    }
}