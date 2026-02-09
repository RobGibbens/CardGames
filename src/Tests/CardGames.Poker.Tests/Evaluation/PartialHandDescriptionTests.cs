using CardGames.Core.French.Cards;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.StudHands;
using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class PartialHandDescriptionTests
{
    [Fact]
    public void TwoCards_HighCard_ReturnsCorrectDescription()
    {
        // Assemble
        var cards = new List<Card>
        {
            new Card(Suit.Hearts, Symbol.Deuce),
            new Card(Suit.Diamonds, Symbol.Ace)
        };
        var hand = new DrawHand(cards);

        // Act
        var result = HandDescriptionFormatter.GetHandDescription(hand);

        // Assert
        result.Should().Be("Ace high");
    }

    [Fact]
    public void ThreeCards_Pair_ReturnsCorrectDescription()
    {
        // Assemble
        var cards = new List<Card>
        {
            new Card(Suit.Hearts, Symbol.Four),
            new Card(Suit.Diamonds, Symbol.Four),
            new Card(Suit.Spades, Symbol.Ace)
        };
        var hand = new DrawHand(cards);

        // Act
        var result = HandDescriptionFormatter.GetHandDescription(hand);

        // Assert
        result.Should().Be("Pair of Fours");
    }

    [Fact]
    public void HoldemHand_TwoCards_ReturnsCorrectDescription()
    {
        // Assemble
        var holeCards = new List<Card>
        {
            new Card(Suit.Hearts, Symbol.King),
            new Card(Suit.Diamonds, Symbol.Queen)
        };
        var communityCards = new List<Card>(); // Empty
        var hand = new HoldemHand(holeCards, communityCards);

        // Act
        var result = HandDescriptionFormatter.GetHandDescription(hand);

        // Assert
        result.Should().Be("King high");
    }

    [Fact]
    public void StudHand_ThreeCards_ReturnsCorrectDescription()
    {
        // Assemble
        var holeCards = new List<Card>
        {
            new Card(Suit.Hearts, Symbol.Ten),
            new Card(Suit.Diamonds, Symbol.Ten)
        };
        var openCards = new List<Card>
        {
             new Card(Suit.Spades, Symbol.Deuce)
        };
        var downCards = new List<Card>();

        var hand = new StudHand(holeCards, openCards, downCards);

        // Act
        var result = HandDescriptionFormatter.GetHandDescription(hand);

        // Assert
        result.Should().Be("Pair of Tens");
    }
}
