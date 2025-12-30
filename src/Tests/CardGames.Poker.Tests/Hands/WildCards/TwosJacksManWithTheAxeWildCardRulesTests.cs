using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.WildCards;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Hands.WildCards;

public class TwosJacksManWithTheAxeWildCardRulesTests
{
    [Fact]
    public void All_Twos_Are_Wild()
    {
        var rules = new TwosJacksManWithTheAxeWildCardRules();
        var hand = "2h 2d 2c 5s 9h".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().HaveCount(3);
        wildCards.Should().OnlyContain(c => c.Symbol == Symbol.Deuce);
    }

    [Fact]
    public void All_Jacks_Are_Wild()
    {
        var rules = new TwosJacksManWithTheAxeWildCardRules();
        var hand = "Jh Jd Jc Js 9h".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().HaveCount(4);
        wildCards.Should().OnlyContain(c => c.Symbol == Symbol.Jack);
    }

    [Fact]
    public void King_Of_Diamonds_Is_Wild()
    {
        var rules = new TwosJacksManWithTheAxeWildCardRules();
        var hand = "Kd 5h 8c 9s Ah".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().HaveCount(1);
        wildCards.Should().Contain(c => c.Symbol == Symbol.King && c.Suit == Suit.Diamonds);
    }

    [Fact]
    public void Other_Kings_Are_Not_Wild()
    {
        var rules = new TwosJacksManWithTheAxeWildCardRules();
        var hand = "Kh Ks Kc 5h 9c".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().BeEmpty();
    }

    [Fact]
    public void Mixed_Wild_Cards_Are_Detected()
    {
        var rules = new TwosJacksManWithTheAxeWildCardRules();
        var hand = "2h Jd Kd 5s 9h".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().HaveCount(3);
        wildCards.Should().Contain(c => c.Symbol == Symbol.Deuce);
        wildCards.Should().Contain(c => c.Symbol == Symbol.Jack);
        wildCards.Should().Contain(c => c.Symbol == Symbol.King && c.Suit == Suit.Diamonds);
    }

    [Fact]
    public void Sevens_Are_Not_Wild()
    {
        var rules = new TwosJacksManWithTheAxeWildCardRules();
        var hand = "7h 7d 7c 7s 9h".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().BeEmpty();
    }

    [Fact]
    public void No_Wild_Cards_In_Regular_Hand()
    {
        var rules = new TwosJacksManWithTheAxeWildCardRules();
        var hand = "3h 5d 8c 9s Ah".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().BeEmpty();
    }

    [Fact]
    public void IsWild_Returns_True_For_Deuce()
    {
        var card = new Card(Suit.Hearts, Symbol.Deuce);

        TwosJacksManWithTheAxeWildCardRules.IsWild(card).Should().BeTrue();
    }

    [Fact]
    public void IsWild_Returns_True_For_Jack()
    {
        var card = new Card(Suit.Spades, Symbol.Jack);

        TwosJacksManWithTheAxeWildCardRules.IsWild(card).Should().BeTrue();
    }

    [Fact]
    public void IsWild_Returns_True_For_King_Of_Diamonds()
    {
        var card = new Card(Suit.Diamonds, Symbol.King);

        TwosJacksManWithTheAxeWildCardRules.IsWild(card).Should().BeTrue();
    }

    [Fact]
    public void IsWild_Returns_False_For_King_Of_Hearts()
    {
        var card = new Card(Suit.Hearts, Symbol.King);

        TwosJacksManWithTheAxeWildCardRules.IsWild(card).Should().BeFalse();
    }

    [Fact]
    public void IsWild_Returns_False_For_Regular_Card()
    {
        var card = new Card(Suit.Clubs, Symbol.Eight);

        TwosJacksManWithTheAxeWildCardRules.IsWild(card).Should().BeFalse();
    }
}
