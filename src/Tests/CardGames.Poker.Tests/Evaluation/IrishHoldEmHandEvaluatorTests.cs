using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Evaluation.Evaluators;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class IrishHoldEmHandEvaluatorTests
{
    private readonly IrishHoldEmHandEvaluator _sut = new();

    #region Hand Creation Tests

    [Fact]
    public void CreateHand_WithHoleAndCommunityCards_ReturnsHoldemHand()
    {
        // Arrange — post-discard Irish player has 2 hole cards + 5 community
        var holeCards = "As Kd".ToCards();
        var communityCards = "Qh Jc Td 5s 2c".ToCards();

        // Act
        var hand = _sut.CreateHand(holeCards, communityCards, []);

        // Assert
        hand.Should().BeOfType<HoldemHand>();
        var holdemHand = (HoldemHand)hand;
        holdemHand.HoleCards.Should().BeEquivalentTo(holeCards);
        holdemHand.CommunityCards.Should().BeEquivalentTo(communityCards);
    }

    [Fact]
    public void CreateHand_WithFlatCardList_SplitsIntoHoleAndCommunity()
    {
        // Arrange — 7 cards: first 2 become hole, remaining 5 become community
        var allCards = "As Kd Qh Jc Td 5s 2c".ToCards();
        var expectedHole = allCards.Take(2).ToList();
        var expectedCommunity = allCards.Skip(2).ToList();

        // Act
        var hand = _sut.CreateHand(allCards);

        // Assert
        hand.Should().BeOfType<HoldemHand>();
        var holdemHand = (HoldemHand)hand;
        holdemHand.HoleCards.Should().BeEquivalentTo(expectedHole);
        holdemHand.CommunityCards.Should().BeEquivalentTo(expectedCommunity);
    }

    #endregion

    #region Evaluator Property Tests

    [Fact]
    public void SupportsPositionalCards_ReturnsTrue()
    {
        _sut.SupportsPositionalCards.Should().BeTrue();
    }

    [Fact]
    public void HasWildCards_ReturnsFalse()
    {
        _sut.HasWildCards.Should().BeFalse();
    }

    [Fact]
    public void GetWildCardIndexes_ReturnsEmpty()
    {
        var cards = "As Kd Qh Jc Td".ToCards();

        var indexes = _sut.GetWildCardIndexes(cards);

        indexes.Should().BeEmpty();
    }

    [Fact]
    public void GetEvaluatedBestCards_ReturnsOriginalCards()
    {
        var holeCards = "As Kd".ToCards();
        var communityCards = "Qh Jc Td 5s 2c".ToCards();
        var hand = new HoldemHand(holeCards, communityCards);

        var bestCards = _sut.GetEvaluatedBestCards(hand);

        bestCards.Should().BeEquivalentTo(hand.Cards);
    }

    #endregion

    #region Hand Evaluation Tests (Hold'Em rules: best 5 from 0-2 hole + community)

    [Theory]
    [InlineData("Ah Kh", "Qh Jh Th 3c 5d", HandType.StraightFlush)]        // Royal Flush (A-K-Q-J-T of hearts)
    [InlineData("As Ad", "Ac Kd Jh 7s 2c", HandType.Trips)]                // Trips using 2 hole + 1 board
    [InlineData("9d 8c", "7h 6s 5d Ac Kh", HandType.Straight)]             // Straight using 2 hole + 3 board
    [InlineData("Ks Kd", "Kh 7c 7d 3s 2h", HandType.FullHouse)]            // Full house
    [InlineData("As Ks", "Qh Jc 9d 4c 2h", HandType.HighCard)]             // High card
    [InlineData("As Ad", "Kh Kc 3s 7d 2h", HandType.TwoPair)]              // Two pair
    [InlineData("As Ad", "Ah Ac 3s 7d 2h", HandType.Quads)]                // Quads
    public void Evaluates_CorrectHandType_WithTwoHoleCards(
        string holeCardStr, string communityCardStr, HandType expectedType)
    {
        var holeCards = holeCardStr.ToCards();
        var communityCards = communityCardStr.ToCards();

        var hand = _sut.CreateHand(holeCards, communityCards, []);

        hand.Type.Should().Be(expectedType);
    }

    [Fact]
    public void HoldemEvaluation_CanUseBoardStraight_WithZeroHoleCards()
    {
        // Hold 'Em allows using 0 hole cards — board straight is valid
        // (Unlike Omaha which requires exactly 2 hole cards)
        var holeCards = "2c 3d".ToCards();
        var communityCards = "6s 7h 8d 9c Ts".ToCards();

        var hand = _sut.CreateHand(holeCards, communityCards, []);

        // Board itself has a straight; Hold'Em can use it (unlike Omaha)
        hand.Type.Should().Be(HandType.Straight);
    }

    [Fact]
    public void HoldemEvaluation_CanUseBoardFlush_WithZeroHoleCards()
    {
        // Hold 'Em allows using 0 hole cards — board flush is valid
        var holeCards = "2d 3c".ToCards();
        var communityCards = "4h 6h 8h Th Kh".ToCards();

        var hand = _sut.CreateHand(holeCards, communityCards, []);

        hand.Type.Should().Be(HandType.Flush);
    }

    [Theory]
    [InlineData("As Kd", "Qh Jc Td 5s 2c")]
    [InlineData("Ks Qd", "Ah Jc Td 5s 2c")]
    public void Determines_Winner_Between_Two_Hands(string holeCardsOne, string holeCardsTwo)
    {
        var board = "Ac Ad Jd 6c 3s".ToCards();
        var handOne = (HoldemHand)_sut.CreateHand(holeCardsOne.ToCards(), board, []);
        var handTwo = (HoldemHand)_sut.CreateHand(holeCardsTwo.ToCards(), board, []);

        // One hand should be ranked higher than the other
        (handOne == handTwo).Should().BeFalse();
    }

    #endregion
}
