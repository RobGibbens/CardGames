using System;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using FluentAssertions;
using Xunit;

namespace CardGames.Core.French.Tests.Cards;

public class SerializationTests
{
    [Theory]
    [InlineData(Suit.Hearts, Symbol.Deuce, "2h")]
    [InlineData(Suit.Hearts, Symbol.Jack, "Jh")]
    [InlineData(Suit.Spades, Symbol.Ace, "As")]
    [InlineData(Suit.Clubs, Symbol.King, "Kc")]
    [InlineData(Suit.Diamonds, Symbol.Ten, "Td")]
    public void Serializes_Card_Correctly(Suit suit, Symbol symbol, string expected)
    {
        var label = new Card(suit, symbol).ToShortString();

        label.Should().Be(expected);
    }

    [Fact]
    public void Serializes_Multiple_Cards_In_Descending_Order()
    {
        var expectedCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Deuce),
            new Card(Suit.Hearts, Symbol.Seven),
            new Card(Suit.Spades, Symbol.Jack),
            new Card(Suit.Clubs, Symbol.King)
        };

        var cardString = expectedCards.ToStringRepresentation();

        cardString.Should().Be("Kc Js 7h 2h");
    }

    [Theory]
    [InlineData("3h", Suit.Hearts, Symbol.Three)]
    [InlineData("8s", Suit.Spades, Symbol.Eight)]
    [InlineData("Kc", Suit.Clubs, Symbol.King)]
    public void Deserializes_Card_Correctly(string expression, Suit expectedSuit, Symbol expectedSymbol)
    {
        var card = expression.ToCard();

        card.Suit.Should().Be(expectedSuit);
        card.Symbol.Should().Be(expectedSymbol);
    }

    [Fact]
    public void Deserializes_Invalid_Card_Throws()
    {
        Action act = () => "XX".ToCard();
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("3h", Suit.Hearts, Symbol.Three)]
    [InlineData("8s", Suit.Spades, Symbol.Eight)]
    [InlineData("Kc", Suit.Clubs, Symbol.King)]
    public void Deserializes_Card_Correctly_Obsolete(string expression, Suit expectedSuit, Symbol expectedSymbol)
    {
        var card = expression.ToCardObsolete();

        card.Suit.Should().Be(expectedSuit);
        card.Symbol.Should().Be(expectedSymbol);
    }

    [Fact]
    public void Deserializes_Invalid_Card_Obsolete_Throws()
    {
        Action act = () => "XX".ToCardObsolete();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deserializes_Cards_Correctly_Obsolete()
    {
        var expression = "Kc Js 7h 2h";
        var expectedCards = new[]
        {
            new Card(Suit.Clubs, Symbol.King),
            new Card(Suit.Spades, Symbol.Jack),
            new Card(Suit.Hearts, Symbol.Seven),
            new Card(Suit.Hearts, Symbol.Deuce)
        };

        var cards = expression.ToCardsObsolete();

        cards.Should().BeEquivalentTo(expectedCards);
    }

    [Fact]
    public void Deserializes_Multiple_Cards_Correctly()
    {
        var expectedCards = new[]
        {
            new Card(Suit.Hearts, Symbol.Deuce),
            new Card(Suit.Hearts, Symbol.Seven),
            new Card(Suit.Spades, Symbol.Jack),
            new Card(Suit.Clubs, Symbol.King)
        };
        
        var cards = "2h 7h Js Kc".ToCards();
        
        cards.Should().BeEquivalentTo(expectedCards);
    }
}
