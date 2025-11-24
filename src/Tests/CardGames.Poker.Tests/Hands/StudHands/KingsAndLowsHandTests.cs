using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace CardGames.Poker.Tests.Hands.StudHands;

public class KingsAndLowsHandTests
{
    [Fact]
    public void Wild_Cards_Are_Identified_Correctly()
    {
        var holeCards = "Kh 2d".ToCards();
        var openCards = "5c 8s Ts Jc".ToCards();
        var downCard = "Qd".ToCard();
        var rules = new WildCardRules(kingRequired: false);

        var hand = new KingsAndLowsHand(holeCards, openCards, downCard, rules);

        hand.WildCards.Should().HaveCount(2);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.King);
        hand.WildCards.Should().Contain(c => c.Symbol == Symbol.Deuce);
    }

    [Fact]
    public void Hand_With_King_And_Low_Card_Improves_To_Five_Of_A_Kind()
    {
        var holeCards = "Kh 2d".ToCards();
        var openCards = "As Ah Ad".ToCards();
        var downCard = "Ac".ToCard();
        var rules = new WildCardRules(kingRequired: false);

        var hand = new KingsAndLowsHand(holeCards, openCards, downCard, rules);

        hand.Type.Should().Be(HandType.FiveOfAKind);
    }

    [Fact]
    public void Hand_Without_Wild_Cards_Evaluates_Normally()
    {
        var holeCards = "As Ah".ToCards();
        var openCards = "3c 5s 7h 9d".ToCards();
        var downCard = "Jc".ToCard();
        var rules = new WildCardRules(kingRequired: true);

        var hand = new KingsAndLowsHand(holeCards, openCards, downCard, rules);

        hand.WildCards.Should().BeEmpty();
        hand.Type.Should().Be(HandType.OnePair);
    }

    [Fact]
    public void King_Required_Variant_Requires_King_For_Wild_Low_Cards()
    {
        var holeCards = "2h 3d".ToCards();
        var openCards = "5c 8s Ts Jc".ToCards();
        var downCard = "Qd".ToCard();
        var rules = new WildCardRules(kingRequired: true);

        var hand = new KingsAndLowsHand(holeCards, openCards, downCard, rules);

        hand.WildCards.Should().BeEmpty();
    }

    [Fact]
    public void King_Required_Variant_With_King_Has_Wild_Cards()
    {
        var holeCards = "Kh 2d".ToCards();
        var openCards = "5c 8s Ts Jc".ToCards();
        var downCard = "Qd".ToCard();
        var rules = new WildCardRules(kingRequired: true);

        var hand = new KingsAndLowsHand(holeCards, openCards, downCard, rules);

        hand.WildCards.Should().HaveCount(2);
    }

    [Fact]
    public void Hand_With_King_Wild_Improves_Hand()
    {
        var wildHoleCards = "Kh 9d".ToCards();
        var naturalHoleCards = "9h 9s".ToCards();
        var openCards = "5c 5s 5h".ToCards();
        var downCard = "8d".ToCard();
        var rules = new WildCardRules(kingRequired: true);

        var wildHand = new KingsAndLowsHand(wildHoleCards, openCards, downCard, rules);
        var naturalHand = new KingsAndLowsHand(naturalHoleCards, openCards, downCard, rules);

        wildHand.Strength.Should().BeGreaterThan(naturalHand.Strength);
    }

    [Fact]
    public void Pair_Of_Lowest_Cards_Are_Both_Wild()
    {
        var holeCards = "2h 2d".ToCards();
        var openCards = "5c 8s Ts Jc".ToCards();
        var downCard = "Qd".ToCard();
        var rules = new WildCardRules(kingRequired: false);

        var hand = new KingsAndLowsHand(holeCards, openCards, downCard, rules);

        hand.WildCards.Should().HaveCount(2);
        hand.WildCards.Should().OnlyContain(c => c.Symbol == Symbol.Deuce);
    }
}
