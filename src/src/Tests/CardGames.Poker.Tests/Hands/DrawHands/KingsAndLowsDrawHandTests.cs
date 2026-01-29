using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.HandTypes;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Hands.DrawHands;

public class KingsAndLowsDrawHandTests
{
    [Fact]
    public void Wild_Cards_Are_Identified_Correctly()
    {
        var cards = "Kh 2d 5c 8s Ts".ToCards();

        var hand = new KingsAndLowsDrawHand(cards);

        hand.WildCards.Should().HaveCount(2);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.King);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Deuce);
    }

    [Fact]
    public void Hand_With_King_And_Low_Card_Improves_To_Five_Of_A_Kind()
    {
        var cards = "Kh 2d As Ah Ad".ToCards();

        var hand = new KingsAndLowsDrawHand(cards);

        hand.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Ace_Treated_As_Low_When_It_Makes_Better_Hand()
    {
        // Hand: 6h, 7d, 8c, 9s, Ah
        // If Ace is HIGH (14): 6 is lowest/wild, hand is 7,8,9,A with one wild - not a straight
        // If Ace is LOW (1): Ace is lowest/wild, hand is 6,7,8,9 with one wild - makes a 10-high straight
        var cards = "6h 7d 8c 9s Ah".ToCards();

        var hand = new KingsAndLowsDrawHand(cards);

        hand.Type.Should().Be(HandType.Straight);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Ace);
    }

    [Fact]
    public void Ace_Treated_As_High_When_It_Makes_Better_Hand()
    {
        // Hand: Ah, Ad, Ac, As, 2h
        // If Ace is HIGH: 2 is lowest/wild, hand is A,A,A,A + 1 wild - Five of a Kind Aces
        // If Ace is LOW: Aces are lowest/wild, but 4 Aces as wild can still make 5-of-a-kind 2s at best
        var cards = "Ah Ad Ac As 2h".ToCards();

        var hand = new KingsAndLowsDrawHand(cards);

        // With Ace high: 2h is wild, 4 Aces + 1 wild = Five of a Kind Aces
        hand.Type.Should().Be(HandType.FiveOfAKind);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Deuce);
    }

    [Fact]
    public void Wheel_Straight_With_Wild_Card()
    {
        // Hand: Ah, 2d, 3c, 4s, 6h
        // 6 is low/wild, can make A-2-3-4-5 wheel straight
        var cards = "Ah 2d 3c 4s 6h".ToCards();

        var hand = new KingsAndLowsDrawHand(cards);

        hand.Type.Should().Be(HandType.Straight);
    }

    [Fact]
    public void Lowest_Card_Is_Wild_Without_King()
    {
        var cards = "2h 5d 8c 9s Ts".ToCards();

        var hand = new KingsAndLowsDrawHand(cards);

        hand.WildCards.Should().HaveCount(1);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Deuce);
    }

    [Fact]
    public void Multiple_Lowest_Cards_Are_All_Wild()
    {
        var cards = "2h 2d 5c 8s Ts".ToCards();

        var hand = new KingsAndLowsDrawHand(cards);

        hand.WildCards.Should().HaveCount(2);
        hand.WildCards.Should().OnlyContain(c => c.Symbol == Symbol.Deuce);
    }
}
