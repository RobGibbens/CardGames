using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.WildCards;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Hands.WildCards;

public class GoodBadUglyWildCardRulesTests
{
    private readonly GoodBadUglyWildCardRules _rules = new();

    [Fact]
    public void DetermineWildCards_WhenNoWildRank_ReturnsEmpty()
    {
        var hand = new Card[]
        {
            new(Suit.Hearts, 10),
            new(Suit.Spades, 11),
            new(Suit.Diamonds, 12)
        };

        var wildCards = _rules.DetermineWildCards(hand, wildRank: null);

        wildCards.Should().BeEmpty();
    }

    [Fact]
    public void DetermineWildCards_WithMatchingRank_ReturnsMatchingCards()
    {
        var hand = new Card[]
        {
            new(Suit.Hearts, 10),
            new(Suit.Spades, 10),
            new(Suit.Diamonds, 12),
            new(Suit.Clubs, 8)
        };

        var wildCards = _rules.DetermineWildCards(hand, wildRank: 10);

        wildCards.Should().HaveCount(2);
        wildCards.Should().AllSatisfy(c => c.Value.Should().Be(10));
    }

    [Fact]
    public void DetermineWildCards_WithNoMatchingRank_ReturnsEmpty()
    {
        var hand = new Card[]
        {
            new(Suit.Hearts, 10),
            new(Suit.Spades, 11),
            new(Suit.Diamonds, 12)
        };

        var wildCards = _rules.DetermineWildCards(hand, wildRank: 5);

        wildCards.Should().BeEmpty();
    }

    [Fact]
    public void DetermineWildCards_AllCardsMatchWildRank_AllAreWild()
    {
        var hand = new Card[]
        {
            new(Suit.Hearts, 7),
            new(Suit.Spades, 7),
            new(Suit.Diamonds, 7),
            new(Suit.Clubs, 7)
        };

        var wildCards = _rules.DetermineWildCards(hand, wildRank: 7);

        wildCards.Should().HaveCount(4);
    }

    [Fact]
    public void DetermineWildCards_SingleMatchingCard_ReturnsOneWild()
    {
        var hand = new Card[]
        {
            new(Suit.Hearts, 14),  // Ace
            new(Suit.Spades, 13),  // King
            new(Suit.Diamonds, 12) // Queen
        };

        var wildCards = _rules.DetermineWildCards(hand, wildRank: 13);

        wildCards.Should().HaveCount(1);
        wildCards.Should().Contain(c => c.Value == 13);
    }
}
