using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class BobBarkerHandDescriptionTests
{
    [Fact]
    public void BobBarkerHand_TwoPairDescription_UsesExactlyTwoHoleCards()
    {
        // Player hole cards (after showcase excluded): Qh, Qc, Kd, Ks
        // Community cards: 6h, 10c, 4h, 2d, 6c
        // Bob Barker must use exactly 2 hole + 3 community (Omaha rules).
        // Best hand is Kd, Ks + 6h, 6c, 10c → Two Pair, Kings and Sixes
        // NOT "Two Pair, Kings and Queens" (which would require 4 hole cards)
        var holeCards = new List<Card>
        {
            new(Suit.Hearts, Symbol.Queen),
            new(Suit.Clubs, Symbol.Queen),
            new(Suit.Diamonds, Symbol.King),
            new(Suit.Spades, Symbol.King)
        };

        var communityCards = new List<Card>
        {
            new(Suit.Hearts, Symbol.Six),
            new(Suit.Clubs, Symbol.Ten),
            new(Suit.Hearts, Symbol.Four),
            new(Suit.Diamonds, Symbol.Deuce),
            new(Suit.Clubs, Symbol.Six)
        };

        var hand = new BobBarkerHand(holeCards, communityCards);

        hand.Type.Should().Be(HandType.TwoPair);

        var description = HandDescriptionFormatter.GetHandDescription(hand);

        description.Should().Be("Two pair, Kings and Sixes");
    }

    [Fact]
    public void BobBarkerHand_EvaluatedBestCards_ContainsExactlyFiveCards()
    {
        var holeCards = new List<Card>
        {
            new(Suit.Hearts, Symbol.Queen),
            new(Suit.Clubs, Symbol.Queen),
            new(Suit.Diamonds, Symbol.King),
            new(Suit.Spades, Symbol.King)
        };

        var communityCards = new List<Card>
        {
            new(Suit.Hearts, Symbol.Six),
            new(Suit.Clubs, Symbol.Ten),
            new(Suit.Hearts, Symbol.Four),
            new(Suit.Diamonds, Symbol.Deuce),
            new(Suit.Clubs, Symbol.Six)
        };

        var hand = new BobBarkerHand(holeCards, communityCards);

        hand.EvaluatedBestCards.Should().HaveCount(5);
    }
}
