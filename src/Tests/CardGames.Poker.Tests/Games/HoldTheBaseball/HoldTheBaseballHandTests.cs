using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Evaluation;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace CardGames.Poker.Tests.Games.HoldTheBaseball;

public class HoldTheBaseballHandTests
{
    [Fact]
    public void Hand_WithNoWildCards_EvaluatesStandardHand()
    {
        // Ace-high straight: A K Q J T
        var holeCards = "As Kh".ToCards();
        var communityCards = "Qd Jc Ts 5h 2c".ToCards();

        var hand = new HoldTheBaseballHand(holeCards, communityCards);

        hand.Type.Should().Be(HandType.Straight);
        hand.WildCards.Should().BeEmpty();
    }

    [Fact]
    public void Hand_WithWildCardInHoleCards_ImprovesHand()
    {
        // Three♠ in hole acts as Ten to complete A-K-Q-J-T straight
        var holeCards = "3s Ah".ToCards();
        var communityCards = "Kd Qc Js 5h 2c".ToCards();

        var hand = new HoldTheBaseballHand(holeCards, communityCards);

        // Wild card should allow at least a straight
        hand.Type.Should().BeOneOf(HandType.Straight, HandType.StraightFlush,
            HandType.Quads, HandType.FiveOfAKind, HandType.FullHouse, HandType.Flush);
        hand.WildCards.Should().HaveCount(1);
        hand.WildCards.First().Symbol.Should().Be(Symbol.Three);
    }

    [Fact]
    public void Hand_WithWildCardInCommunityCards_ImprovesHand()
    {
        // Pair of Aces in hole + Three♦ wild in community can become Ace
        var holeCards = "As Ah".ToCards();
        var communityCards = "3d Kc Qs 5h 2c".ToCards();

        var hand = new HoldTheBaseballHand(holeCards, communityCards);

        // With two natural aces and a wild 3, should be at least trips
        hand.Type.Should().BeOneOf(HandType.Trips, HandType.Quads, HandType.FullHouse, HandType.FiveOfAKind);
        hand.WildCards.Should().HaveCount(1);
    }

    [Fact]
    public void Hand_WithMultipleWildCards_MakesFiveOfAKind()
    {
        // Two 3s and two 9s wild + Ace → five Aces
        var holeCards = "3s 9h".ToCards();
        var communityCards = "3d 9c As Kh 2c".ToCards();

        var hand = new HoldTheBaseballHand(holeCards, communityCards);

        hand.Type.Should().Be(HandType.FiveOfAKind);
        hand.WildCards.Should().HaveCount(4);
    }

    [Fact]
    public void Nine_IsWildCard()
    {
        // 9♦ in hole is wild, with three natural Aces → four of a kind or better
        var holeCards = "9d As".ToCards();
        var communityCards = "Ah Ad 5c 7h 2c".ToCards();

        var hand = new HoldTheBaseballHand(holeCards, communityCards);

        hand.WildCards.Should().HaveCount(1);
        hand.WildCards.First().Symbol.Should().Be(Symbol.Nine);
        hand.Type.Should().BeOneOf(HandType.Quads, HandType.FiveOfAKind);
    }

    [Fact]
    public void AllWildsInCommunity_AreDetected()
    {
        // Community has 3♣, 9♠, 3♥ — three wilds
        var holeCards = "As Kh".ToCards();
        var communityCards = "3c 9s 3h 5d 2c".ToCards();

        var hand = new HoldTheBaseballHand(holeCards, communityCards);

        hand.WildCards.Should().HaveCount(3);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Three);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Nine);
    }

    [Fact]
    public void WildCardHand_BeatsEquivalentNaturalHand()
    {
        var communityCards = "Qd Jc 7s 5h 2c".ToCards();

        // Natural pair of Aces
        var naturalHand = new HoldTheBaseballHand("As Ah".ToCards(), communityCards);
        // Wild 3 + Ace: wild can act as Ace to make pair or better, improving hand
        var wildHand = new HoldTheBaseballHand("3s Ah".ToCards(), communityCards);

        // The wild hand with a 3 (acting as another Ace) should make a pair of Aces too,
        // and the natural hand is also a pair of Aces. Both are at least OnePair.
        naturalHand.Type.Should().Be(HandType.OnePair);
        wildHand.Type.Should().BeOneOf(HandType.OnePair, HandType.TwoPair,
            HandType.Trips, HandType.Straight, HandType.Flush, HandType.FullHouse,
            HandType.Quads, HandType.FiveOfAKind, HandType.StraightFlush);
        // Wild card flexibility means the hand should be at least as strong
        wildHand.Strength.Should().BeGreaterThanOrEqualTo(naturalHand.Strength);
    }

    [Fact]
    public void WildCards_Property_ReturnsCorrectWildCards()
    {
        var holeCards = "3h 9d".ToCards();
        var communityCards = "5c 8s Ts Jc Qd".ToCards();

        var hand = new HoldTheBaseballHand(holeCards, communityCards);

        hand.WildCards.Should().HaveCount(2);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Three);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Nine);
    }

    [Fact]
    public void EvaluatedBestCards_ReturnsCards()
    {
        var holeCards = "3h As".ToCards();
        var communityCards = "Kd Qc Js 5h 2c".ToCards();

        var hand = new HoldTheBaseballHand(holeCards, communityCards);

        hand.EvaluatedBestCards.Should().NotBeNullOrEmpty();
        hand.EvaluatedBestCards.Should().HaveCount(5);
    }

    [Fact]
    public void Hand_WithNoWildCards_EvaluatedBestCards_AreOriginalCards()
    {
        var holeCards = "As Kh".ToCards();
        var communityCards = "Qd Jc Ts 5h 2c".ToCards();

        var hand = new HoldTheBaseballHand(holeCards, communityCards);

        hand.WildCards.Should().BeEmpty();
        hand.EvaluatedBestCards.Should().HaveCount(5);
        // All evaluated cards should come from the original 7 cards
        var allCards = holeCards.Concat(communityCards).ToList();
        hand.EvaluatedBestCards.Should().AllSatisfy(c => allCards.Should().Contain(c));
    }

    [Theory]
    [InlineData("2s 5d", "8d Js Kc 5c 5h", HandType.Trips)]
    [InlineData("As Kh", "Qd Jc Ts 5h 2c", HandType.Straight)]
    [InlineData("As Ah", "Ad Kc Qs 5h 2c", HandType.Trips)]
    public void Hand_WithNoWildCards_DeterminesCorrectHandType(
        string holeStr, string communityStr, HandType expectedType)
    {
        var hand = new HoldTheBaseballHand(holeStr.ToCards(), communityStr.ToCards());

        hand.WildCards.Should().BeEmpty();
        hand.Type.Should().Be(expectedType);
    }

    [Fact]
    public void Repro_Hole5sAd_FlopJh3hAc_EvaluatesTripsAces_AndFormatsDescription()
    {
        var hand = new HoldTheBaseballHand("5s Ad".ToCards(), "Jh 3h Ac".ToCards());

        hand.Type.Should().Be(HandType.Trips);
        HandDescriptionFormatter.GetHandDescription(hand).Should().Be("Three of a kind, Aces");
    }
}
