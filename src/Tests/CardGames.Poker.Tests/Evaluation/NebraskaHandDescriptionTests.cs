using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class NebraskaHandDescriptionTests
{
    [Fact]
    public void NebraskaHand_StraightDescription_UsesExactlyThreeHoleCards()
    {
        // Player hole: 4h, 5d, 8d, Jc, Jh
        // Board: 9s, 7c, Jd, 6d, 3c
        // Nebraska must use exactly 3 hole + 2 board.
        // Correct best straight is 4-5-6-7-8 (to the Eight), not 5-6-7-8-9.
        var holeCards = new List<Card>
        {
            new(Suit.Hearts, Symbol.Four),
            new(Suit.Diamonds, Symbol.Five),
            new(Suit.Diamonds, Symbol.Eight),
            new(Suit.Clubs, Symbol.Jack),
            new(Suit.Hearts, Symbol.Jack)
        };

        var communityCards = new List<Card>
        {
            new(Suit.Spades, Symbol.Nine),
            new(Suit.Clubs, Symbol.Seven),
            new(Suit.Diamonds, Symbol.Jack),
            new(Suit.Diamonds, Symbol.Six),
            new(Suit.Clubs, Symbol.Three)
        };

        var hand = new NebraskaHand(holeCards, communityCards);

        var description = HandDescriptionFormatter.GetHandDescription(hand);

        description.Should().Be("Straight to the Eight");
    }
}
