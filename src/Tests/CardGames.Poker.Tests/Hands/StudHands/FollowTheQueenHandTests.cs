using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.StudHands;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace CardGames.Poker.Tests.Hands.StudHands;

public class FollowTheQueenHandTests
{
    [Fact]
    public void Wild_Cards_Are_Identified_Correctly_With_Queen_And_Following()
    {
        var holeCards = "Kh 2d".ToCards();
        var openCards = "Qh 5c 8s Ts".ToCards();
        var downCard = "Jc".ToCard();
        var faceUpCards = "Qh 5c 8s Ts".ToCards(); // Q then 5 - 5s are wild

        var hand = new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards);

        hand.WildCards.Should().HaveCount(2); // Qh, 5c
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Queen);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Five);
    }

    [Fact]
    public void Hand_With_Wild_Cards_Improves_To_Five_Of_A_Kind()
    {
        var holeCards = "Qh 5d".ToCards();
        var openCards = "As Ah Ad Ac".ToCards();
        var downCard = "2c".ToCard();
        var faceUpCards = "Qh 5d As Ah".ToCards(); // Q then 5 - 5s are wild

        var hand = new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards);

        // With Q (wild) and 5 (wild), can make 5 Aces
        hand.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Hand_Without_Wild_Cards_Evaluates_Normally()
    {
        var holeCards = "As Ah".ToCards();
        var openCards = "3c 5s 7h 9d".ToCards();
        var downCard = "Jc".ToCard();
        var faceUpCards = "3c 5s 7h 9d".ToCards(); // No Queens

        var hand = new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards);

        hand.WildCards.Should().BeEmpty();
        hand.Type.Should().Be(HandType.OnePair);
    }

    [Fact]
    public void Queens_In_Hand_Are_Always_Wild()
    {
        var holeCards = "Qh Qs".ToCards();
        var openCards = "5c 8s Ts Jc".ToCards();
        var downCard = "Kd".ToCard();
        var faceUpCards = "5c 8s Ts Jc".ToCards(); // No Queens face up

        var hand = new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards);

        hand.WildCards.Should().HaveCount(2);
        hand.WildCards.Should().OnlyContain(c => c.Symbol == Symbol.Queen);
    }

    [Fact]
    public void Queen_As_Last_Face_Up_Only_Queens_Wild()
    {
        var holeCards = "Qh 5d".ToCards();
        var openCards = "3c 8s Ts Qc".ToCards();
        var downCard = "Kd".ToCard();
        var faceUpCards = "3c 8s Ts Qc".ToCards(); // Queen is last face-up

        var hand = new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards);

        hand.WildCards.Should().HaveCount(2); // Both Queens
        hand.WildCards.Should().OnlyContain(c => c.Symbol == Symbol.Queen);
    }

    [Fact]
    public void Second_Queen_Changes_Wild_Rank()
    {
        var holeCards = "5h 8d".ToCards();
        var openCards = "Qh 5d Qc 8s".ToCards();
        var downCard = "Kd".ToCard();
        var faceUpCards = "Qh 5d Qc 8s".ToCards(); // Q->5, Q->8, so 8s are wild, not 5s

        var hand = new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards);

        hand.WildCards.Should().HaveCount(4); // Qh, Qc, 8d, 8s
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Queen);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Eight);
        hand.WildCards.Should().NotContain(c => c.Symbol == Symbol.Five);
    }

    [Fact]
    public void Hand_With_Queen_Wild_Makes_Straight_Flush()
    {
        var holeCards = "Qh Kh".ToCards();
        var openCards = "Ah Jh Th 2c".ToCards();
        var downCard = "3d".ToCard();
        var faceUpCards = "Ah Jh Th 2c".ToCards(); // No Queen face-up

        var hand = new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards);

        // Qh is wild, can complete royal flush A K Q J T of hearts
        hand.Type.Should().Be(HandType.StraightFlush);
    }

    [Fact]
    public void Multiple_Wild_Cards_Can_Make_Strong_Hand()
    {
        var holeCards = "Qh Qs".ToCards();
        var openCards = "Qc 5d As Ah".ToCards();
        var downCard = "Ad".ToCard();
        var faceUpCards = "Qc 5d As Ah".ToCards(); // Q->5, so Q and 5 wild

        var hand = new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards);

        // Three Queens wild + 5 wild = 4 wilds, can make Five of a Kind Aces
        hand.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Constructor_Throws_When_Not_Two_Hole_Cards()
    {
        var holeCards = "Kh".ToCards();
        var openCards = "5c 8s Ts Jc".ToCards();
        var downCard = "Qd".ToCard();
        var faceUpCards = "5c 8s Ts Jc".ToCards();

        var action = () => new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("holeCards");
    }

    [Fact]
    public void Constructor_Throws_When_More_Than_Four_Open_Cards()
    {
        var holeCards = "Kh 2d".ToCards();
        var openCards = "5c 8s Ts Jc Ac".ToCards();
        var downCard = "Qd".ToCard();
        var faceUpCards = "5c 8s Ts Jc Ac".ToCards();

        var action = () => new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards);

        action.Should().Throw<ArgumentException>()
            .WithParameterName("openCards");
    }

    [Fact]
    public void Hand_With_Wild_Cards_Beats_Same_Natural_Hand()
    {
        var faceUpCards = "Qh 5d 8s Ts".ToCards();

        // Hand with wild Queen making quads
        var wildHoleCards = "Qh Ad".ToCards();
        var wildOpenCards = "As Ah Ac 8c".ToCards();
        var wildDownCard = "2d".ToCard();

        // Natural hand with trips
        var naturalHoleCards = "3h 4d".ToCards();
        var naturalOpenCards = "As Ah Ac 8c".ToCards();
        var naturalDownCard = "2d".ToCard();

        var wildHand = new FollowTheQueenHand(wildHoleCards, wildOpenCards, wildDownCard, faceUpCards);
        var naturalHand = new FollowTheQueenHand(naturalHoleCards, naturalOpenCards, naturalDownCard, faceUpCards);

        wildHand.Strength.Should().BeGreaterThan(naturalHand.Strength);
    }

    [Fact]
    public void All_Seven_Cards_Are_Available()
    {
        var holeCards = "Kh 2d".ToCards();
        var openCards = "5c 8s Ts Jc".ToCards();
        var downCard = "Qd".ToCard();
        var faceUpCards = "5c 8s Ts Jc".ToCards();

        var hand = new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards);

        hand.Cards.Should().HaveCount(7);
    }

    [Fact]
    public void Wild_Five_Rank_With_Multiple_Fives_In_Hand()
    {
        var holeCards = "5h 5s".ToCards();
        var openCards = "Qh 5d 8s Ts".ToCards();
        var downCard = "Jc".ToCard();
        var faceUpCards = "Qh 5d 8s Ts".ToCards(); // Q->5, 5s wild

        var hand = new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards);

        hand.WildCards.Should().HaveCount(4); // Qh, 5h, 5s, 5d
        hand.WildCards.Count(c => c.Symbol == Symbol.Five).Should().Be(3);
    }

    [Fact]
    public void Hand_With_Only_Queen_Wild_Improves()
    {
        var holeCards = "Qh Kd".ToCards();
        var openCards = "Ks Kh Kc 2c".ToCards();
        var downCard = "3d".ToCard();
        var faceUpCards = "Ks Kh Kc 2c".ToCards(); // No Queen face-up

        var hand = new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards);

        // With wild Queen and 4 Kings, can make Five of a Kind Kings
        hand.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Face_Up_Cards_Order_Matters_For_Wild_Determination()
    {
        var holeCards = "5h 8d".ToCards();
        var openCards = "Qh 5d 8s Ts".ToCards();
        var downCard = "Jc".ToCard();

        // Different face-up card orders lead to different wild cards
        var faceUpCards1 = "Qh 5d 8s Ts".ToCards(); // Q->5, 5s wild
        var faceUpCards2 = "5d Qh 8s Ts".ToCards(); // Q->8, 8s wild

        var hand1 = new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards1);
        var hand2 = new FollowTheQueenHand(holeCards, openCards, downCard, faceUpCards2);

        hand1.WildCards.Should().Contain(c => c.Symbol == Symbol.Five);
        hand2.WildCards.Should().Contain(c => c.Symbol == Symbol.Eight);
    }
}
