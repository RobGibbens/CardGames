using CardGames.Poker.Hands.HandTypes;
using CardGames.Core.French.Cards.Extensions;
using Xunit;
using FluentAssertions;
using CardGames.Poker.Hands.StudHands;
using System.Linq;

namespace CardGames.Poker.Tests.Hands;

public class BaseballHandTests
{
    /// <summary>
    /// Tests that a hand without wild cards is evaluated normally.
    /// </summary>
    [Theory]
    [InlineData("2s 5d 7h Kc Qd Jh 8c", HandType.HighCard)]
    [InlineData("2s 5d 7h Kc Qd Kh 8c", HandType.OnePair)]
    [InlineData("2s 5d 5h Kc Qd Kh 8c", HandType.TwoPair)]
    [InlineData("2s 5d 5h 5c Qd Kh 8c", HandType.Trips)]
    [InlineData("4s 5d 6h 7c 8d Kh Ac", HandType.Straight)] // 4,5,6,7,8 straight
    [InlineData("2s 5s 6s Ts As Kh 8c", HandType.Flush)] // Spade flush (not consecutive)
    [InlineData("2s 5d 5h 5c 8d 8h Ac", HandType.FullHouse)]
    [InlineData("5s 5d 5h 5c 8d Kh Ac", HandType.Quads)]
    [InlineData("4s 5s 6s 7s 8s Kh Ac", HandType.StraightFlush)]
    public void Determines_Hand_Type_Without_Wild_Cards(string cardString, HandType expectedHandType)
    {
        var cards = cardString.ToCards();
        var hand = new BaseballHand(
            cards.Take(2).ToList(),
            cards.Skip(2).Take(4).ToList(),
            cards.Skip(6).ToList());

        hand.Type.Should().Be(expectedHandType);
    }

    /// <summary>
    /// Tests that 3s and 9s act as wild cards.
    /// A single wild card can improve a pair to trips.
    /// </summary>
    [Theory]
    [InlineData("3s 5d 5h Kc Qd Jh 8c", HandType.Trips)] // 3 is wild, makes trip 5s
    [InlineData("9s 5d 5h Kc Qd Jh 8c", HandType.Trips)] // 9 is wild, makes trip 5s
    public void Wild_Card_Improves_Pair_To_Trips(string cardString, HandType expectedHandType)
    {
        var cards = cardString.ToCards();
        var hand = new BaseballHand(
            cards.Take(2).ToList(),
            cards.Skip(2).Take(4).ToList(),
            cards.Skip(6).ToList());

        hand.Type.Should().Be(expectedHandType);
    }

    /// <summary>
    /// Tests that wild cards can complete a flush.
    /// Hand must not be able to make a straight or better without the flush.
    /// </summary>
    [Theory]
    [InlineData("3s 2s As Ts Ks Jh 8c", HandType.Flush)] // 3 is wild, makes spade flush (5 spades total)
    [InlineData("9h 2h Ah Th Kh Js 8c", HandType.Flush)] // 9 is wild, makes heart flush (5 hearts total)
    public void Wild_Card_Completes_Flush(string cardString, HandType expectedHandType)
    {
        var cards = cardString.ToCards();
        var hand = new BaseballHand(
            cards.Take(2).ToList(),
            cards.Skip(2).Take(4).ToList(),
            cards.Skip(6).ToList());

        hand.Type.Should().Be(expectedHandType);
    }

    /// <summary>
    /// Tests that wild cards can complete a straight.
    /// </summary>
    [Theory]
    [InlineData("3s 5d 6h 7c 8d Kh Ac", HandType.Straight)] // 3 wild becomes 4 or 9 for straight (4-5-6-7-8 or 5-6-7-8-9)
    [InlineData("9s 5d 6h 7c 8d Kh Ac", HandType.Straight)] // 9 wild fits into 5-6-7-8-9 straight
    public void Wild_Card_Completes_Straight(string cardString, HandType expectedHandType)
    {
        var cards = cardString.ToCards();
        var hand = new BaseballHand(
            cards.Take(2).ToList(),
            cards.Skip(2).Take(4).ToList(),
            cards.Skip(6).ToList());

        hand.Type.Should().Be(expectedHandType);
    }

    /// <summary>
    /// Tests that with enough wild cards, Five of a Kind is possible.
    /// </summary>
    [Theory]
    [InlineData("3s 3d As Ah Ac 9h 9c", HandType.FiveOfAKind)] // 4 wild cards + triple Aces = 5 Aces
    [InlineData("3s 3d 3h Ah Ac 9h 9c", HandType.FiveOfAKind)] // 5 wild cards can make 5 of anything
    [InlineData("3s 3d 3h 3c Ac 9h 9c", HandType.FiveOfAKind)] // 6 wild cards
    public void Multiple_Wild_Cards_Make_Five_Of_A_Kind(string cardString, HandType expectedHandType)
    {
        var cards = cardString.ToCards();
        var hand = new BaseballHand(
            cards.Take(2).ToList(),
            cards.Skip(2).Take(4).ToList(),
            cards.Skip(6).ToList());

        hand.Type.Should().Be(expectedHandType);
    }

    /// <summary>
    /// Tests that wild cards improve a pair to quads.
    /// </summary>
    [Theory]
    [InlineData("3s 3d Ah Ac Kd Qh Jc", HandType.Quads)] // 2 wilds + pair of Aces = 4 Aces
    [InlineData("9s 9d Ah Ac Kd Qh Jc", HandType.Quads)] // 2 wilds + pair of Aces = 4 Aces
    public void Two_Wild_Cards_With_Pair_Make_Quads(string cardString, HandType expectedHandType)
    {
        var cards = cardString.ToCards();
        var hand = new BaseballHand(
            cards.Take(2).ToList(),
            cards.Skip(2).Take(4).ToList(),
            cards.Skip(6).ToList());

        hand.Type.Should().Be(expectedHandType);
    }

    /// <summary>
    /// Tests that Five of a Kind beats Straight Flush.
    /// </summary>
    [Fact]
    public void Five_Of_A_Kind_Beats_Straight_Flush()
    {
        // Five of a Kind with wild cards
        var fiveOfAKindCards = "3s 3d As Ah Ac 9h Kc".ToCards();
        var fiveOfAKind = new BaseballHand(
            fiveOfAKindCards.Take(2).ToList(),
            fiveOfAKindCards.Skip(2).Take(4).ToList(),
            fiveOfAKindCards.Skip(6).ToList());

        // Natural Straight Flush (no wild cards)
        var straightFlushCards = "2s 4s 5s 6s 7s 8s Kd".ToCards();
        var straightFlush = new BaseballHand(
            straightFlushCards.Take(2).ToList(),
            straightFlushCards.Skip(2).Take(4).ToList(),
            straightFlushCards.Skip(6).ToList());

        fiveOfAKind.Type.Should().Be(HandType.FiveOfAKind);
        straightFlush.Type.Should().Be(HandType.StraightFlush);
        fiveOfAKind.Strength.Should().BeGreaterThan(straightFlush.Strength);
    }

    /// <summary>
    /// Tests that higher Five of a Kind beats lower Five of a Kind.
    /// </summary>
    [Fact]
    public void Higher_Five_Of_A_Kind_Beats_Lower()
    {
        // Five Aces
        var fiveAcesCards = "3s 3d As Ah Ac 9h 9c".ToCards();
        var fiveAces = new BaseballHand(
            fiveAcesCards.Take(2).ToList(),
            fiveAcesCards.Skip(2).Take(4).ToList(),
            fiveAcesCards.Skip(6).ToList());

        // Five Kings
        var fiveKingsCards = "3s 3d Ks Kh Kc 9h 9c".ToCards();
        var fiveKings = new BaseballHand(
            fiveKingsCards.Take(2).ToList(),
            fiveKingsCards.Skip(2).Take(4).ToList(),
            fiveKingsCards.Skip(6).ToList());

        fiveAces.Type.Should().Be(HandType.FiveOfAKind);
        fiveKings.Type.Should().Be(HandType.FiveOfAKind);
        fiveAces.Strength.Should().BeGreaterThan(fiveKings.Strength);
    }

    /// <summary>
    /// Tests that a natural quads beats wild-improved trips.
    /// </summary>
    [Fact]
    public void Quads_Beat_Trips()
    {
        // Quads (natural)
        var quadsCards = "Ks Kd Kh Kc 2d 5h 7c".ToCards();
        var quads = new BaseballHand(
            quadsCards.Take(2).ToList(),
            quadsCards.Skip(2).Take(4).ToList(),
            quadsCards.Skip(6).ToList());

        // Trips with wild card improvement
        var tripsCards = "As Ad 3h 5c 7d 8h Tc".ToCards();
        var trips = new BaseballHand(
            tripsCards.Take(2).ToList(),
            tripsCards.Skip(2).Take(4).ToList(),
            tripsCards.Skip(6).ToList());

        quads.Type.Should().Be(HandType.Quads);
        trips.Type.Should().Be(HandType.Trips);
        quads.Strength.Should().BeGreaterThan(trips.Strength);
    }
}
