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

    [Fact]
    public void GetCardColor_WithoutWildCards_ReturnsSuitColor()
    {
        // Arrange
        var heartCard = "Ah".ToCard();
        var spadeCard = "As".ToCard();
        
        // Act
        var heartColor = CardAsciiArt.GetCardColor(heartCard);
        var spadeColor = CardAsciiArt.GetCardColor(spadeCard);
        
        // Assert
        heartColor.Should().Be("red");
        spadeColor.Should().Be("white");
    }

    [Fact]
    public void GetCardColor_WildCard_ReturnsGreen()
    {
        // Arrange
        var card = "9h".ToCard();
        var wildCards = new[] { card };
        
        // Act
        var color = CardAsciiArt.GetCardColor(card, wildCards);
        
        // Assert
        color.Should().Be("green");
    }

    [Fact]
    public void GetCardColor_NonWildCard_ReturnsSuitColor()
    {
        // Arrange
        var card = "Ah".ToCard();
        var wildCard = "9s".ToCard();
        var wildCards = new[] { wildCard };
        
        // Act
        var color = CardAsciiArt.GetCardColor(card, wildCards);
        
        // Assert
        color.Should().Be("red");
    }

    [Theory]
    [InlineData("3h", "green")]  // Wild 3 of hearts - should be green instead of red
    [InlineData("9s", "green")]  // Wild 9 of spades - should be green instead of white
    [InlineData("3c", "green")]  // Wild 3 of clubs - should be green instead of white
    [InlineData("9d", "green")]  // Wild 9 of diamonds - should be green instead of red
    public void GetCardColor_WildCard_ReturnsGreenRegardlessOfSuit(string cardString, string expectedColor)
    {
        // Arrange
        var card = cardString.ToCard();
        var wildCards = new[] { card };
        
        // Act
        var color = CardAsciiArt.GetCardColor(card, wildCards);
        
        // Assert
        color.Should().Be(expectedColor);
    }

    [Fact]
    public void GetCardColor_NullWildCards_ReturnsSuitColor()
    {
        // Arrange
        var card = "Ah".ToCard();
        
        // Act
        var color = CardAsciiArt.GetCardColor(card, null);
        
        // Assert
        color.Should().Be("red");
    }

    [Fact]
    public void GetCardColor_EmptyWildCards_ReturnsSuitColor()
    {
        // Arrange
        var card = "Ah".ToCard();
        var wildCards = System.Array.Empty<Card>();
        
        // Act
        var color = CardAsciiArt.GetCardColor(card, wildCards);
        
        // Assert
        color.Should().Be("red");
    }
}
