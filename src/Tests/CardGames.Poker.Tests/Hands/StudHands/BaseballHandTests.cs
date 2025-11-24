using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace CardGames.Poker.Tests.Hands.StudHands;

public class BaseballHandTests
{
    [Fact]
    public void Wild_Cards_Are_Threes_And_Nines()
    {
        var holeCards = "3h 9d".ToCards();
        var openCards = "5c 8s Ts Jc".ToCards();
        var downCard = "Qd".ToCard();

        var hand = new BaseballHand(holeCards, openCards, new[] { downCard });

        hand.WildCards.Should().HaveCount(2);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Three);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Nine);
    }

    [Fact]
    public void Hand_With_Wild_Cards_Improves_To_Five_Of_A_Kind()
    {
        var holeCards = "3h 9d".ToCards();
        var openCards = "As Ah Ad".ToCards();
        var downCard = "Ac".ToCard();

        var hand = new BaseballHand(holeCards, openCards, new[] { downCard });

        hand.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Hand_Without_Wild_Cards_Evaluates_Normally()
    {
        var holeCards = "As Ah".ToCards();
        var openCards = "5c 6s 7h 8d".ToCards();
        var downCard = "Jc".ToCard();

        var hand = new BaseballHand(holeCards, openCards, new[] { downCard });

        hand.WildCards.Should().BeEmpty();
        hand.Type.Should().Be(HandType.OnePair);
    }

    [Fact]
    public void Multiple_Threes_Are_All_Wild()
    {
        var holeCards = "3h 3d".ToCards();
        var openCards = "5c 8s Ts Jc".ToCards();
        var downCard = "Qd".ToCard();

        var hand = new BaseballHand(holeCards, openCards, new[] { downCard });

        hand.WildCards.Should().HaveCount(2);
        hand.WildCards.Should().OnlyContain(c => c.Symbol == Symbol.Three);
    }

    [Fact]
    public void Multiple_Nines_Are_All_Wild()
    {
        var holeCards = "9h 9d".ToCards();
        var openCards = "5c 8s Ts Jc".ToCards();
        var downCard = "Qd".ToCard();

        var hand = new BaseballHand(holeCards, openCards, new[] { downCard });

        hand.WildCards.Should().HaveCount(2);
        hand.WildCards.Should().OnlyContain(c => c.Symbol == Symbol.Nine);
    }

    [Fact]
    public void Hand_With_Wild_Three_Makes_Quads()
    {
        var holeCards = "3h Kd".ToCards();
        var openCards = "Ks Kh Kc 5c".ToCards();
        var downCard = "2d".ToCard();

        var hand = new BaseballHand(holeCards, openCards, new[] { downCard });

        // With a wild 3, can make five Kings (Five of a Kind)
        hand.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Hand_With_Wild_Nine_Improves_Hand()
    {
        var holeCards = "9h As".ToCards();
        var openCards = "Ah Ad Ac 5c".ToCards();
        var downCard = "2d".ToCard();

        var hand = new BaseballHand(holeCards, openCards, new[] { downCard });

        // With a wild 9 and four Aces, can make Five of a Kind Aces
        hand.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Hand_Can_Have_More_Than_Seven_Cards()
    {
        // In Baseball, 4s give extra cards, so players can have 8+ cards
        var holeCards = "As Ah".ToCards();
        var openCards = "4c Kc Qc Jc Tc".ToCards(); // 5 open cards
        var downCards = "9c".ToCards(); // 1 down card (the 4 gave an extra card)

        var hand = new BaseballHand(holeCards, openCards, downCards);

        hand.Cards.Should().HaveCount(8);
        hand.WildCards.Should().HaveCount(1); // Just the 9
    }

    [Fact]
    public void Wild_Card_Can_Complete_Straight_Flush()
    {
        var holeCards = "3h Kh".ToCards();
        var openCards = "Ah Qh Jh Th".ToCards();
        var downCard = "2d".ToCard();

        var hand = new BaseballHand(holeCards, openCards, new[] { downCard });

        // 3h is wild, can be used as 9h to make royal flush (A K Q J T of hearts)
        // Or the 3h could just complete Ah Kh Qh Jh Th as a straight flush
        hand.Type.Should().Be(HandType.StraightFlush);
    }
}
