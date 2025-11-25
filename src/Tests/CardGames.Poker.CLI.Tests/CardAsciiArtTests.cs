using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.CLI.Output;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.CLI.Tests;

public class CardAsciiArtTests
{
    [Fact]
    public void GetCardFace_ReturnsCorrectNumberOfLines()
    {
        // Arrange
        var card = "Ah".ToCard();
        
        // Act
        var lines = CardAsciiArt.GetCardFace(card);
        
        // Assert
        lines.Should().HaveCount(CardAsciiArt.Height);
    }

    [Fact]
    public void GetCardFace_ContainsSymbol()
    {
        // Arrange
        var card = "Kc".ToCard();
        
        // Act
        var lines = CardAsciiArt.GetCardFace(card);
        var combined = string.Join("", lines);
        
        // Assert
        combined.Should().Contain("K");
    }

    [Fact]
    public void GetCardFace_ContainsSuitSymbol()
    {
        // Arrange
        var card = "7h".ToCard();
        
        // Act
        var lines = CardAsciiArt.GetCardFace(card);
        var combined = string.Join("", lines);
        
        // Assert
        combined.Should().Contain("♥");
    }

    [Fact]
    public void GetCardBack_ReturnsCorrectNumberOfLines()
    {
        // Act
        var lines = CardAsciiArt.GetCardBack();
        
        // Assert
        lines.Should().HaveCount(CardAsciiArt.Height);
    }

    [Fact]
    public void GetCardBack_ContainsBackPattern()
    {
        // Act
        var lines = CardAsciiArt.GetCardBack();
        var combined = string.Join("", lines);
        
        // Assert
        combined.Should().Contain("░");
    }

    [Theory]
    [InlineData(Suit.Hearts, "red")]
    [InlineData(Suit.Diamonds, "red")]
    [InlineData(Suit.Spades, "white")]
    [InlineData(Suit.Clubs, "white")]
    public void GetSuitColor_ReturnsCorrectColor(Suit suit, string expectedColor)
    {
        // Act
        var color = CardAsciiArt.GetSuitColor(suit);
        
        // Assert
        color.Should().Be(expectedColor);
    }

    [Theory]
    [InlineData(Suit.Hearts, "♥")]
    [InlineData(Suit.Diamonds, "♦")]
    [InlineData(Suit.Spades, "♠")]
    [InlineData(Suit.Clubs, "♣")]
    public void GetSuitChar_ReturnsCorrectCharacter(Suit suit, string expectedChar)
    {
        // Act
        var suitChar = CardAsciiArt.GetSuitChar(suit);
        
        // Assert
        suitChar.Should().Be(expectedChar);
    }

    [Theory]
    [InlineData(Symbol.Ace, "A")]
    [InlineData(Symbol.King, "K")]
    [InlineData(Symbol.Queen, "Q")]
    [InlineData(Symbol.Jack, "J")]
    [InlineData(Symbol.Ten, "10")]
    [InlineData(Symbol.Nine, "9")]
    [InlineData(Symbol.Deuce, "2")]
    public void GetSymbolChar_ReturnsCorrectCharacter(Symbol symbol, string expectedChar)
    {
        // Act
        var symbolChar = CardAsciiArt.GetSymbolChar(symbol);
        
        // Assert
        symbolChar.Should().Be(expectedChar);
    }

    [Fact]
    public void Width_ReturnsPositiveValue()
    {
        // Act
        var width = CardAsciiArt.Width;
        
        // Assert
        width.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Height_ReturnsPositiveValue()
    {
        // Act
        var height = CardAsciiArt.Height;
        
        // Assert
        height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetCardFace_TenCard_HasCorrectSymbol()
    {
        // Arrange - Test Ten card which has 2 characters ("10")
        var card = "Ts".ToCard();
        
        // Act
        var lines = CardAsciiArt.GetCardFace(card);
        var combined = string.Join("", lines);
        
        // Assert
        combined.Should().Contain("10");
    }

    [Fact]
    public void GetCardFace_AllLines_HaveSameWidth()
    {
        // Arrange
        var card = "Qd".ToCard();
        
        // Act
        var lines = CardAsciiArt.GetCardFace(card);
        
        // Assert
        lines.Should().AllSatisfy(line => 
            line.Length.Should().Be(CardAsciiArt.Width));
    }
}
