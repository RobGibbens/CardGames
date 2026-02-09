using CardGames.Core.French.Cards;
using System;
using System.Collections.Generic;

namespace CardGames.Poker.Hands.StudHands;

public class SevenCardStudHand : StudHand
{
    public SevenCardStudHand(
        IReadOnlyCollection<Card> holeCards,
        IReadOnlyCollection<Card> openCards,
        Card downCard) : base(holeCards, openCards, new[] { downCard })
    {
    }
}
