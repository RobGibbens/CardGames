using CardGames.Poker.Shared;
using CardGames.Poker.Shared.Events;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Shared.Tests;

public class ChipDenominationServiceTests
{
    [Fact]
    public void ConvertToChipStack_ZeroAmount_ReturnsEmptyStack()
    {
        // Act
        var result = ChipDenominationService.ConvertToChipStack(0);

        // Assert
        result.TotalAmount.Should().Be(0);
        result.Chips.Should().BeEmpty();
    }

    [Fact]
    public void ConvertToChipStack_SingleDenomination_ReturnsCorrectStack()
    {
        // Act
        var result = ChipDenominationService.ConvertToChipStack(100);

        // Assert
        result.TotalAmount.Should().Be(100);
        result.Chips.Should().HaveCount(1);
        result.Chips[0].Denomination.Should().Be(100);
        result.Chips[0].Count.Should().Be(1);
    }

    [Fact]
    public void ConvertToChipStack_MultipleDenominations_ReturnsCorrectBreakdown()
    {
        // Act - 125 = 100 + 25
        var result = ChipDenominationService.ConvertToChipStack(125);

        // Assert
        result.TotalAmount.Should().Be(125);
        result.Chips.Should().HaveCount(2);
        
        // Should be sorted from largest to smallest
        result.Chips[0].Denomination.Should().Be(100);
        result.Chips[0].Count.Should().Be(1);
        result.Chips[1].Denomination.Should().Be(25);
        result.Chips[1].Count.Should().Be(1);
    }

    [Fact]
    public void ConvertToChipStack_LargeAmount_UsesLargestDenominations()
    {
        // Act - 10500 = 10000 + 500
        var result = ChipDenominationService.ConvertToChipStack(10500);

        // Assert
        result.TotalAmount.Should().Be(10500);
        result.Chips.Should().Contain(c => c.Denomination == 10000);
        result.Chips.Should().Contain(c => c.Denomination == 500);
    }

    [Fact]
    public void ConvertToChipStack_MultipleOfSameDenomination_ReturnsCorrectCount()
    {
        // Act - 300 = 3 x 100
        var result = ChipDenominationService.ConvertToChipStack(300);

        // Assert
        result.TotalAmount.Should().Be(300);
        result.Chips.Should().HaveCount(1);
        result.Chips[0].Denomination.Should().Be(100);
        result.Chips[0].Count.Should().Be(3);
    }

    [Fact]
    public void ConvertToChipStack_SmallAmount_UsesSmallDenominations()
    {
        // Act - 7 = 5 + 1 + 1
        var result = ChipDenominationService.ConvertToChipStack(7);

        // Assert
        result.TotalAmount.Should().Be(7);
        result.Chips.Should().Contain(c => c.Denomination == 5 && c.Count == 1);
        result.Chips.Should().Contain(c => c.Denomination == 1 && c.Count == 2);
    }

    [Fact]
    public void ConvertToChipStack_NegativeAmount_ThrowsException()
    {
        // Act
        var act = () => ChipDenominationService.ConvertToChipStack(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ConvertToSimplifiedChipStack_LimitsDenominations()
    {
        // Arrange - Use an amount that would normally require many denominations
        var amount = 1111; // Would need 1000+100+10+1

        // Act
        var result = ChipDenominationService.ConvertToSimplifiedChipStack(amount, maxDenominations: 2);

        // Assert
        result.TotalAmount.Should().Be(amount);
        result.Chips.Count.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void GetColorForDenomination_StandardDenomination_ReturnsCorrectColor()
    {
        // Act
        var whiteColor = ChipDenominationService.GetColorForDenomination(1);
        var redColor = ChipDenominationService.GetColorForDenomination(5);
        var blackColor = ChipDenominationService.GetColorForDenomination(100);

        // Assert
        whiteColor.Should().Be("#FFFFFF"); // White for $1
        redColor.Should().Be("#FF0000");   // Red for $5
        blackColor.Should().Be("#000000"); // Black for $100
    }

    [Fact]
    public void GetColorForDenomination_UnknownDenomination_ReturnsDefaultColor()
    {
        // Act
        var color = ChipDenominationService.GetColorForDenomination(999);

        // Assert
        color.Should().Be("#808080"); // Gray default
    }

    [Theory]
    [InlineData(1, "$1")]
    [InlineData(100, "$100")]
    [InlineData(1000, "$1K")]
    [InlineData(5000, "$5K")]
    [InlineData(1000000, "$1M")]
    public void GetDenominationLabel_ReturnsCorrectFormat(int denomination, string expected)
    {
        // Act
        var result = ChipDenominationService.GetDenominationLabel(denomination);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ConvertBlindToChipStack_ReturnsValidStack()
    {
        // Act
        var result = ChipDenominationService.ConvertBlindToChipStack(25);

        // Assert
        result.TotalAmount.Should().Be(25);
        result.Chips.Should().NotBeEmpty();
    }

    [Fact]
    public void StandardDenominations_ContainsExpectedValues()
    {
        // Assert
        ChipDenominationService.StandardDenominations.Should().NotBeEmpty();
        ChipDenominationService.StandardDenominations.Should().Contain(d => d.Value == 1);
        ChipDenominationService.StandardDenominations.Should().Contain(d => d.Value == 5);
        ChipDenominationService.StandardDenominations.Should().Contain(d => d.Value == 25);
        ChipDenominationService.StandardDenominations.Should().Contain(d => d.Value == 100);
    }

    [Fact]
    public void ConvertToChipStack_TotalAmountMatchesInput()
    {
        // Arrange
        var amounts = new[] { 1, 5, 10, 25, 50, 100, 500, 1000, 5000, 12345, 99999 };

        foreach (var amount in amounts)
        {
            // Act
            var result = ChipDenominationService.ConvertToChipStack(amount);

            // Assert - verify total matches
            var calculatedTotal = result.Chips.Sum(c => c.Denomination * c.Count);
            calculatedTotal.Should().Be(amount, $"for input amount {amount}");
        }
    }
}
