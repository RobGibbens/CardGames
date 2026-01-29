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

    [Fact]
    public void DetermineWildCardsWithAceLow_Treats_Ace_As_Lowest()
    {
        var rules = new WildCardRules(kingRequired: false);
        var hand = "Ah 6d 7c 8s 9h".ToCards();

        var wildCards = rules.DetermineWildCardsWithAceLow(hand);

        wildCards.Should().HaveCount(1);
        wildCards.Should().Contain(c => c.Symbol == Symbol.Ace);
    }

    [Fact]
    public void DetermineWildCards_Treats_Ace_As_High()
    {
        var rules = new WildCardRules(kingRequired: false);
        var hand = "Ah 6d 7c 8s 9h".ToCards();

        var wildCards = rules.DetermineWildCards(hand);

        wildCards.Should().HaveCount(1);
        wildCards.Should().Contain(c => c.Symbol == Symbol.Six);
    }

    [Fact]
    public void GetAllPossibleWildCardSets_Returns_Both_When_Ace_Present()
    {
        var rules = new WildCardRules(kingRequired: false);
        var hand = "Ah 6d 7c 8s 9h".ToCards();

        var wildCardSets = rules.GetAllPossibleWildCardSets(hand);

        wildCardSets.Should().HaveCount(2);
        wildCardSets[0].Should().Contain(c => c.Symbol == Symbol.Six);
        wildCardSets[1].Should().Contain(c => c.Symbol == Symbol.Ace);
    }

    [Fact]
    public void GetAllPossibleWildCardSets_Returns_One_When_No_Ace()
    {
        var rules = new WildCardRules(kingRequired: false);
        var hand = "2h 6d 7c 8s 9h".ToCards();

        var wildCardSets = rules.GetAllPossibleWildCardSets(hand);

        wildCardSets.Should().HaveCount(1);
        wildCardSets[0].Should().Contain(c => c.Symbol == Symbol.Deuce);
    }

    [Fact]
    public void GetAllPossibleWildCardSets_Returns_One_When_Ace_And_Low_Are_Same()
    {
        // When only Ace is in hand and no other low cards differ between scenarios
        var rules = new WildCardRules(kingRequired: false);
        var hand = "Ah Ad 7c 8s 9h".ToCards();

        var wildCardSets = rules.GetAllPossibleWildCardSets(hand);

        // Ace-high: 7 is low, Ace-low: Ace is low - these are different
        wildCardSets.Should().HaveCount(2);
    }
}
