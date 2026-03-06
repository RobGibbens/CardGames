using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Evaluation.Evaluators;
using CardGames.Poker.Hands.CommunityCardHands;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Evaluation;

public class HoldemHandEvaluatorTests
{
    private readonly HoldemHandEvaluator _sut = new();

    [Fact]
    public void CreateHand_WithHoleAndCommunityCards_ReturnsHoldemHand()
    {
        // Arrange
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
}
