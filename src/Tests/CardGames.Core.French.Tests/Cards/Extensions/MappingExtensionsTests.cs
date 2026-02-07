using System;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using FluentAssertions;
using Xunit;

namespace CardGames.Core.French.Tests.Cards.Extensions;

public class MappingExtensionsTests
{
    [Theory]
    [InlineData(2, Symbol.Deuce)]
    [InlineData(3, Symbol.Three)]
    [InlineData(4, Symbol.Four)]
    [InlineData(5, Symbol.Five)]
    [InlineData(6, Symbol.Six)]
    [InlineData(7, Symbol.Seven)]
    [InlineData(8, Symbol.Eight)]
    [InlineData(9, Symbol.Nine)]
    [InlineData(10, Symbol.Ten)]
    [InlineData(11, Symbol.Jack)]
    [InlineData(12, Symbol.Queen)]
    [InlineData(13, Symbol.King)]
    [InlineData(14, Symbol.Ace)]
    public void ToSymbol_Int_Returns_Correct_Symbol(int value, Symbol expected)
    {
        value.ToSymbol().Should().Be(expected);
    }

    [Fact]
    public void ToSymbol_Int_Throws_On_Invalid_Value()
    {
        Action act = () => 15.ToSymbol();
        act.Should().Throw<ArgumentException>()
           .WithMessage("*not a valid card value*");
    }

    [Theory]
    [InlineData('2', Symbol.Deuce)]
    [InlineData('3', Symbol.Three)]
    [InlineData('4', Symbol.Four)]
    [InlineData('5', Symbol.Five)]
    [InlineData('6', Symbol.Six)]
    [InlineData('7', Symbol.Seven)]
    [InlineData('8', Symbol.Eight)]
    [InlineData('9', Symbol.Nine)]
    [InlineData('T', Symbol.Ten)]
    [InlineData('J', Symbol.Jack)]
    [InlineData('Q', Symbol.Queen)]
    [InlineData('K', Symbol.King)]
    [InlineData('A', Symbol.Ace)]
    public void ToSymbol_Char_Returns_Correct_Symbol(char value, Symbol expected)
    {
        value.ToSymbol().Should().Be(expected);
    }

    [Fact]
    public void ToSymbol_Char_Throws_On_Invalid_Value()
    {
        Action act = () => 'X'.ToSymbol();
        act.Should().Throw<ArgumentException>()
           .WithMessage("*not a valid card value character*");
    }

    [Theory]
    [InlineData('h', Suit.Hearts)]
    [InlineData('d', Suit.Diamonds)]
    [InlineData('c', Suit.Clubs)]
    [InlineData('s', Suit.Spades)]
    public void ToSuit_Char_Returns_Correct_Suit(char suit, Suit expected)
    {
        suit.ToSuit().Should().Be(expected);
    }

    [Fact]
    public void ToSuit_Char_Throws_On_Invalid_Suit()
    {
        Action act = () => 'x'.ToSuit();
        act.Should().Throw<ArgumentException>()
           .WithMessage("*not a valid card suit character*");
    }
}
