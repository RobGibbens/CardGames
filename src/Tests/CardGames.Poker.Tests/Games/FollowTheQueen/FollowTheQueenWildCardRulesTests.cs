using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games.FollowTheQueen;

public class FollowTheQueenWildCardRulesTests
{
    private readonly FollowTheQueenWildCardRules _rules = new();

    [Fact]
    public void DetermineWildCards_WhenQueenFollowedByJack_JackShouldBeWild()
    {
        // Arrange
        // Face up cards: Queen of Diamonds, Jack of Spades
        var faceUpCards = new List<Card>
        {
            new Card(Suit.Diamonds, Symbol.Queen),
            new Card(Suit.Spades, Symbol.Jack)
        };

        // Hand: 7h, 8s, Js
        var hand = new List<Card>
        {
            new Card(Suit.Hearts, Symbol.Seven),
            new Card(Suit.Spades, Symbol.Eight),
            new Card(Suit.Spades, Symbol.Jack)
        };

        // Act
        var wildCards = _rules.DetermineWildCards(hand, faceUpCards);

        // Assert
        // Queens are always wild
        // Jack should be wild because it followed a Queen
        wildCards.Should().Contain(c => c.Symbol == Symbol.Jack);
        wildCards.Should().HaveCount(1); // Only the Jack in hand is wild
    }

    [Fact]
    public void DetermineWildRanks_WhenQueenFollowedByJack_JackRankShouldBeReturned()
    {
        // Arrange
        var faceUpCards = new List<Card>
        {
            new Card(Suit.Diamonds, Symbol.Queen),
            new Card(Suit.Spades, Symbol.Jack)
        };

        // Act
        var wildRanks = _rules.DetermineWildRanks(faceUpCards);

        // Assert
        wildRanks.Should().Contain((int)Symbol.Queen);
        wildRanks.Should().Contain((int)Symbol.Jack);
    }

    [Fact]
    public void HandEvaluation_WhenJackIsWild_ShouldBePairOfEights()
    {
        // Arrange
        // Rob's hand: 7h, 8s (hole), Js (board)
        // We are simulating the hand evaluation at this point (3rd street)
        // Note: FollowTheQueenHand expects 2 hole cards.
        var holeCards = new List<Card>
        {
            new Card(Suit.Hearts, Symbol.Seven),
            new Card(Suit.Spades, Symbol.Eight)
        };
        var boardCards = new List<Card>
        {
            new Card(Suit.Spades, Symbol.Jack)
        };
        var faceUpCards = new List<Card>
        {
            new Card(Suit.Diamonds, Symbol.Queen),
            new Card(Suit.Spades, Symbol.Jack)
        };

        // Constructor: hole, open, downCard, faceUpCardsInOrder
        // downCard is usually the 7th street card. Can be null for partial hands?
        // Constructor says: downCard != null ? ... : Array.Empty<Card>()
        // If we are at 3rd street, downCard is null.

        // However, FollowTheQueenHand is normally evaluated at Showdown (7 cards).
        // If evaluated early, it should still work with fewer cards.

        var hand = new FollowTheQueenHand(holeCards, boardCards, null, faceUpCards);

        // Act
        // Accessing EvaluatedBestCards or Type triggers evaluation
        var type = hand.Type;
        var bestCards = hand.EvaluatedBestCards;

        // Assert
        // With 7h, 8s, Js(Wild).
        // Wild can be anything.
        // It should match 8s to make Pair of 8s.
        // Or match 7h to make Pair of 7s.
        // Pair of 8s is better.
        // Or if it could be Ace High? Pair is better than High Card.

        type.Should().Be(CardGames.Poker.Hands.HandTypes.HandType.OnePair);
        bestCards.Should().Contain(c => c.Symbol == Symbol.Eight);
    }
}
