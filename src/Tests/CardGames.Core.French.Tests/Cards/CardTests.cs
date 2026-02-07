using CardGames.Core.French.Cards;
using FluentAssertions;
using Xunit;

namespace CardGames.Core.French.Tests.Cards;

public class CardTests
{
    [Theory]
    [InlineData(2, Symbol.Deuce)]
    [InlineData(9, Symbol.Nine)]
    [InlineData(11, Symbol.Jack)]
    [InlineData(14, Symbol.Ace)]
    public void Determines_Symbol_From_Value(int value, Symbol expectedSymbol)
    {
        var card = new Card(Suit.Hearts, value);

        card.Symbol.Should().Be(expectedSymbol);
    }

    [Theory]
    [InlineData(Symbol.Deuce, 2)]
    [InlineData(Symbol.Nine, 9)]
    [InlineData(Symbol.Jack, 11)]
    [InlineData(Symbol.Ace, 14)]
    public void Determines_Value_From_Symbol(Symbol symbol, int expectedValue)
    {
        var card = new Card(Suit.Hearts, symbol);

        card.Value.Should().Be(expectedValue);
    }

    [Fact]
    public void Equality_Check()
    {
        var card1 = new Card(Suit.Hearts, Symbol.Ace);
        var card2 = new Card(Suit.Hearts, Symbol.Ace);
        var card3 = new Card(Suit.Spades, Symbol.Ace);
        var card4 = new Card(Suit.Hearts, Symbol.King);

        card1.Should().Be(card2);
        card1.Should().NotBe(card3);
        card1.Should().NotBe(card4);

        (card1 == card2).Should().BeTrue();
        (card1 != card2).Should().BeFalse();
        (card1 == card3).Should().BeFalse();
        (card1 != card3).Should().BeTrue();

        Card nullCard = null;
        (card1 == nullCard).Should().BeFalse();
        (nullCard == card1).Should().BeFalse();
        (nullCard == nullCard).Should().BeTrue();
    }
}
