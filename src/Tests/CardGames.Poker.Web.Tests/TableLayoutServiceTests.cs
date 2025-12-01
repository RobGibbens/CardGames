using CardGames.Poker.Web.Utilities;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Web.Tests;

public class TableLayoutServiceTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void GetPositions_ReturnsCorrectNumberOfPositions(int playerCount)
    {
        // Act
        var positions = TableLayoutService.GetPositions(playerCount);

        // Assert
        positions.Should().HaveCount(playerCount);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(10)]
    public void GetPositions_AllPositionsHaveUniqueSeatNumbers(int playerCount)
    {
        // Act
        var positions = TableLayoutService.GetPositions(playerCount);

        // Assert
        var seatNumbers = positions.Select(p => p.SeatNumber).ToList();
        seatNumbers.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(9)]
    public void GetPositions_AllPositionsWithinTableBounds(int playerCount)
    {
        // Act
        var positions = TableLayoutService.GetPositions(playerCount);

        // Assert
        foreach (var position in positions)
        {
            position.Top.Should().BeInRange(0, 100);
            position.Left.Should().BeInRange(0, 100);
            position.BetTop.Should().BeInRange(0, 100);
            position.BetLeft.Should().BeInRange(0, 100);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(9)]
    public void GetPositions_AlignmentIsValid(int playerCount)
    {
        // Act
        var positions = TableLayoutService.GetPositions(playerCount);

        // Assert
        foreach (var position in positions)
        {
            position.Alignment.Should().BeOneOf("center", "left", "right");
        }
    }

    [Fact]
    public void GetPositions_ThrowsForInvalidPlayerCount_TooLow()
    {
        // Act
        var act = () => TableLayoutService.GetPositions(1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetPositions_ThrowsForInvalidPlayerCount_TooHigh()
    {
        // Act
        var act = () => TableLayoutService.GetPositions(11);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1, 9)]
    [InlineData(5, 9)]
    [InlineData(9, 9)]
    public void GetPosition_ReturnsValidPositionForSeat(int seatNumber, int totalSeats)
    {
        // Act
        var position = TableLayoutService.GetPosition(seatNumber, totalSeats);

        // Assert
        position.Should().NotBeNull();
        position.SeatNumber.Should().Be(seatNumber);
    }

    [Fact]
    public void GetPotPosition_ReturnsCenterPosition()
    {
        // Act
        var (top, left) = TableLayoutService.GetPotPosition();

        // Assert
        top.Should().Be(50);
        left.Should().Be(50);
    }

    [Fact]
    public void GetCommunityCardsPosition_ReturnsValidPosition()
    {
        // Act
        var (top, left) = TableLayoutService.GetCommunityCardsPosition();

        // Assert
        top.Should().BeInRange(40, 50);
        left.Should().Be(50);
    }

    [Theory]
    [InlineData(90, 50, "Bottom center")]
    [InlineData(10, 50, "Top center")]
    [InlineData(50, 10, "Left side")]
    [InlineData(50, 90, "Right side")]
    public void CalculateBetPosition_MovesTowardCenter(double playerTop, double playerLeft, string description)
    {
        // Act
        var (betTop, betLeft) = TableLayoutService.CalculateBetPosition(playerTop, playerLeft);

        // Assert - bet position should be closer to center (50, 50) than player position
        var playerDistanceFromCenter = Math.Sqrt(Math.Pow(playerTop - 50, 2) + Math.Pow(playerLeft - 50, 2));
        var betDistanceFromCenter = Math.Sqrt(Math.Pow(betTop - 50, 2) + Math.Pow(betLeft - 50, 2));
        
        betDistanceFromCenter.Should().BeLessThan(playerDistanceFromCenter, 
            $"Bet position for {description} should be closer to center");
    }

    [Theory]
    [InlineData("mobile")]
    [InlineData("tablet")]
    [InlineData("desktop")]
    [InlineData("large")]
    public void GetResponsiveScaleVariables_ReturnsValidCssVariables(string breakpoint)
    {
        // Act
        var variables = TableLayoutService.GetResponsiveScaleVariables(breakpoint);

        // Assert
        variables.Should().Contain("--table-scale:");
        variables.Should().Contain("--card-scale:");
        variables.Should().Contain("--chip-scale:");
    }

    [Fact]
    public void GetResponsiveScaleVariables_MobileHasSmallestScale()
    {
        // Act
        var mobileVars = TableLayoutService.GetResponsiveScaleVariables("mobile");
        var desktopVars = TableLayoutService.GetResponsiveScaleVariables("desktop");

        // Assert
        mobileVars.Should().Contain("0.6");
        desktopVars.Should().Contain("1.0");
    }

    [Theory]
    [InlineData(2, "Heads-up")]
    [InlineData(6, "6-max")]
    [InlineData(9, "9-max")]
    public void GetPositions_HeroAtBottomCenter(int playerCount, string description)
    {
        // Act
        var positions = TableLayoutService.GetPositions(playerCount);
        var heroPosition = positions.First(p => p.SeatNumber == 1);

        // Assert - Hero (seat 1) should be at the bottom of the table
        heroPosition.Top.Should().BeGreaterThan(80, $"Hero position for {description} should be at bottom");
        heroPosition.Left.Should().BeInRange(40, 60, $"Hero position for {description} should be centered");
    }
}
