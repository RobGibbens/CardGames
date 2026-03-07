using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation.Evaluators;
using CardGames.Poker.Hands.CommunityCardHands;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games.HoldTheBaseball;

public class HoldTheBaseballHandEvaluatorTests
{
    private readonly HoldTheBaseballHandEvaluator _sut = new();

    [Fact]
    public void CreateHand_WithFlatCardList_SplitsIntoHoleAndCommunity()
    {
        // 7 cards: first 2 become hole, remaining 5 become community
        var allCards = "As Kd Qh Jc Td 5s 2c".ToCards();
        var expectedHole = allCards.Take(2).ToList();
        var expectedCommunity = allCards.Skip(2).ToList();

        var hand = _sut.CreateHand(allCards);

        hand.Should().BeOfType<HoldTheBaseballHand>();
        var holdTheBaseballHand = (HoldTheBaseballHand)hand;
        holdTheBaseballHand.HoleCards.Should().BeEquivalentTo(expectedHole);
        holdTheBaseballHand.CommunityCards.Should().BeEquivalentTo(expectedCommunity);
    }

    [Fact]
    public void CreateHand_WithPositionalArgs_ReturnsHoldTheBaseballHand()
    {
        var holeCards = "As Kd".ToCards();
        var communityCards = "Qh Jc Td 5s 2c".ToCards();

        var hand = _sut.CreateHand(holeCards, communityCards, []);

        hand.Should().BeOfType<HoldTheBaseballHand>();
        var holdTheBaseballHand = (HoldTheBaseballHand)hand;
        holdTheBaseballHand.HoleCards.Should().BeEquivalentTo(holeCards);
        holdTheBaseballHand.CommunityCards.Should().BeEquivalentTo(communityCards);
    }

    [Fact]
    public void GetWildCardIndexes_ReturnsIndexesForThreesAndNines()
    {
        // Index: 0=3s, 1=As, 2=9d, 3=Kh, 4=Qc
        var cards = "3s As 9d Kh Qc".ToCards();

        var indexes = _sut.GetWildCardIndexes(cards);

        indexes.Should().HaveCount(2);
        indexes.Should().Contain(0); // 3s
        indexes.Should().Contain(2); // 9d
    }

    [Fact]
    public void GetWildCardIndexes_WithNoWildCards_ReturnsEmpty()
    {
        var cards = "As Kd Qh Jc Td".ToCards();

        var indexes = _sut.GetWildCardIndexes(cards);

        indexes.Should().BeEmpty();
    }

    [Fact]
    public void GetWildCardIndexes_WithMultipleThreesAndNines_ReturnsAll()
    {
        // Index: 0=3s, 1=3h, 2=9d, 3=9c, 4=As
        var cards = "3s 3h 9d 9c As".ToCards();

        var indexes = _sut.GetWildCardIndexes(cards);

        indexes.Should().HaveCount(4);
        indexes.Should().Contain(new[] { 0, 1, 2, 3 });
    }

    [Fact]
    public void GetEvaluatedBestCards_ReturnsEvaluatedCards()
    {
        var holeCards = "3h As".ToCards();
        var communityCards = "Kd Qc Js 5h 2c".ToCards();
        var hand = new HoldTheBaseballHand(holeCards, communityCards);

        var bestCards = _sut.GetEvaluatedBestCards(hand);

        bestCards.Should().NotBeNullOrEmpty();
        bestCards.Should().HaveCount(5);
    }

    [Fact]
    public void GetEvaluatedBestCards_WithNoWildCards_ReturnsOriginalBestCards()
    {
        var holeCards = "As Kd".ToCards();
        var communityCards = "Qh Jc Td 5s 2c".ToCards();
        var hand = new HoldTheBaseballHand(holeCards, communityCards);

        var bestCards = _sut.GetEvaluatedBestCards(hand);

        bestCards.Should().HaveCount(5);
        // Should be A-K-Q-J-T straight
        var allCards = holeCards.Concat(communityCards).ToList();
        bestCards.Should().AllSatisfy(c => allCards.Should().Contain(c));
    }

    [Fact]
    public void HasWildCards_ReturnsTrue()
    {
        _sut.HasWildCards.Should().BeTrue();
    }

    [Fact]
    public void SupportsPositionalCards_ReturnsTrue()
    {
        _sut.SupportsPositionalCards.Should().BeTrue();
    }
}
