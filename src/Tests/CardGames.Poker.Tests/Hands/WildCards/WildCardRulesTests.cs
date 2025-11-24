using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Hands.WildCards;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Hands.WildCards;

public class WildCardRulesTests
{
    [Fact]
    public void Kings_Are_Always_Wild()
    {
        var rules = new WildCardRules(kingRequired: false);
        var hand = "Kh 5d 8c 9s Ts Jc Qd".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().HaveCount(2);
        wildCards.Should().Contain(c => c.Symbol == Symbol.King);
        wildCards.Should().Contain(c => c.Symbol == Symbol.Five);
    }

    [Fact]
    public void Multiple_Kings_Are_All_Wild()
    {
        var rules = new WildCardRules(kingRequired: false);
        var hand = "Kh Kd 5c 8s Ts Jc Qd".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().HaveCount(3);
        wildCards.Should().Contain(c => c.Symbol == Symbol.King && c.Suit == Suit.Hearts);
        wildCards.Should().Contain(c => c.Symbol == Symbol.King && c.Suit == Suit.Diamonds);
        wildCards.Should().Contain(c => c.Symbol == Symbol.Five);
    }

    [Fact]
    public void Lowest_Card_Is_Wild()
    {
        var rules = new WildCardRules(kingRequired: false);
        var hand = "2h 5d 8c 9s Ts Jc Qd".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().HaveCount(1);
        wildCards.Should().Contain(c => c.Symbol == Symbol.Deuce);
    }

    [Fact]
    public void Multiple_Lowest_Cards_Are_All_Wild()
    {
        var rules = new WildCardRules(kingRequired: false);
        var hand = "2h 2d 5c 8s Kh Jc Qd".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().HaveCount(3);
        wildCards.Should().Contain(c => c.Symbol == Symbol.Deuce && c.Suit == Suit.Hearts);
        wildCards.Should().Contain(c => c.Symbol == Symbol.Deuce && c.Suit == Suit.Diamonds);
        wildCards.Should().Contain(c => c.Symbol == Symbol.King);
    }

    [Fact]
    public void King_And_Lowest_Card_Are_Both_Wild()
    {
        var rules = new WildCardRules(kingRequired: false);
        var hand = "Kh 2d 5c 8s Ts Jc Qd".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().HaveCount(2);
        wildCards.Should().Contain(c => c.Symbol == Symbol.King);
        wildCards.Should().Contain(c => c.Symbol == Symbol.Deuce);
    }

    [Fact]
    public void KingRequired_LowCards_Not_Wild_Without_King()
    {
        var rules = new WildCardRules(kingRequired: true);
        var hand = "2h 5d 8c 9s Ts Jc Qd".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().BeEmpty();
    }

    [Fact]
    public void KingRequired_LowCards_Are_Wild_With_King()
    {
        var rules = new WildCardRules(kingRequired: true);
        var hand = "Kh 2d 5c 8s Ts Jc Qd".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().HaveCount(2);
        wildCards.Should().Contain(c => c.Symbol == Symbol.King);
        wildCards.Should().Contain(c => c.Symbol == Symbol.Deuce);
    }

    [Fact]
    public void King_Is_Not_Counted_As_Lowest_Card()
    {
        var rules = new WildCardRules(kingRequired: false);
        var hand = "Kh Kd 5c 8s Ts Jc Qd".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().HaveCount(3);
        wildCards.Should().Contain(c => c.Symbol == Symbol.King && c.Suit == Suit.Hearts);
        wildCards.Should().Contain(c => c.Symbol == Symbol.King && c.Suit == Suit.Diamonds);
        wildCards.Should().Contain(c => c.Symbol == Symbol.Five);
    }
}
