using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Hands.DrawHands;

public class TwosJacksManWithTheAxeDrawHandTests
{
    [Fact]
    public void Hand_Without_Wild_Cards_Evaluates_Normally()
    {
        var cards = "Ah Kh Qh 9c 8c".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.WildCards.Should().BeEmpty();
        hand.Type.Should().Be(HandType.HighCard);
    }

    [Fact]
    public void Single_Deuce_Creates_Pair()
    {
        var cards = "2h Ah Qh 9c 8c".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.WildCards.Should().HaveCount(1);
        hand.Type.Should().Be(HandType.OnePair);
    }

    [Fact]
    public void Two_Deuces_Create_Trips()
    {
        var cards = "2h 2d Ah 9c 8c".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.WildCards.Should().HaveCount(2);
        hand.Type.Should().Be(HandType.Trips);
    }

    [Fact]
    public void Jack_Is_Wild()
    {
        var cards = "Jh Ah Qh 9c 8c".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.WildCards.Should().HaveCount(1);
        hand.Type.Should().Be(HandType.OnePair);
    }

    [Fact]
    public void King_Of_Diamonds_Is_Wild()
    {
        var cards = "Kd Ah Qh 9c 8c".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.WildCards.Should().HaveCount(1);
        hand.Type.Should().Be(HandType.OnePair);
    }

    [Fact]
    public void King_Of_Hearts_Is_Not_Wild()
    {
        var cards = "Kh Ah Qh 9c 8c".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.WildCards.Should().BeEmpty();
        hand.Type.Should().Be(HandType.HighCard);
    }

    [Fact]
    public void All_Wild_Cards_Create_Five_Of_A_Kind()
    {
        // 4 Jacks + 1 Deuce = 5 wilds
        var cards = "Jh Jd Jc Js 2h".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.WildCards.Should().HaveCount(5);
        hand.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Mixed_Wild_Cards_Create_Strong_Hand()
    {
        // 2 + Jack + Kd = 3 wilds + Ace + King = at least Quads, but may be higher (wild card evaluator optimizes)
        var cards = "2h Jd Kd Ah Kh".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.WildCards.Should().HaveCount(3);
        // With 3 wild cards and Ace + King, the evaluator may find straight flush or better
        ((int)hand.Type).Should().BeGreaterThanOrEqualTo((int)HandType.Quads);
    }

    [Fact]
    public void HasNaturalPairOfSevens_Returns_True_With_Two_Sevens()
    {
        var cards = "7h 7d Ah Kh Qh".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.HasNaturalPairOfSevens().Should().BeTrue();
    }

    [Fact]
    public void HasNaturalPairOfSevens_Returns_False_With_One_Seven()
    {
        var cards = "7h Ah Kh Qh Jc".ToCards(); // Jack is wild
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.HasNaturalPairOfSevens().Should().BeFalse();
    }

    [Fact]
    public void HasNaturalPairOfSevens_Returns_False_With_No_Sevens()
    {
        var cards = "Ah Kh Qh 9c 8c".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.HasNaturalPairOfSevens().Should().BeFalse();
    }

    [Fact]
    public void HasNaturalPairOfSevens_Returns_True_With_Multiple_Sevens()
    {
        var cards = "7h 7d 7c 7s Ah".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.HasNaturalPairOfSevens().Should().BeTrue();
    }

    [Fact]
    public void Hand_With_Wild_Cards_Has_Higher_Strength_Than_Without()
    {
        var regularPair = new TwosJacksManWithTheAxeDrawHand("Ah Ad Qh 9c 8c".ToCards());
        var wildTrips = new TwosJacksManWithTheAxeDrawHand("2h Ah Ad 9c 8c".ToCards());

        wildTrips.Strength.Should().BeGreaterThan(regularPair.Strength);
        wildTrips.Type.Should().Be(HandType.Trips);
        regularPair.Type.Should().Be(HandType.OnePair);
    }

    [Fact]
    public void EvaluatedBestCards_Returns_Five_Cards()
    {
        var cards = "2h Ah Kh Qh 9c".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.EvaluatedBestCards.Should().HaveCount(5);
    }

    [Fact]
    public void Wild_Card_Can_Complete_Straight()
    {
        // 5, 6, 7, 8 + 2 (wild as 4 or 9) = Straight
        var cards = "5h 6d 7c 8s 2h".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.Type.Should().Be(HandType.Straight);
    }

    [Fact]
    public void Wild_Card_Can_Complete_Flush()
    {
        // 4 hearts + Jack (wild) = can complete a flush
        var cards = "Ah Kh Qh 9h Jd".ToCards();
        var hand = new TwosJacksManWithTheAxeDrawHand(cards);

        hand.Type.Should().Be(HandType.Flush);
    }
}
