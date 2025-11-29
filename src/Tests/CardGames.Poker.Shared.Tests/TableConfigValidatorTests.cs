using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.Validation;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Shared.Tests;

public class TableConfigValidatorTests
{
    [Fact]
    public void Validate_ValidConfig_ReturnsNoErrors()
    {
        // Arrange
        var config = CreateValidConfig();

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_ValidConfig_ReturnsTrue()
    {
        // Arrange
        var config = CreateValidConfig();

        // Act
        var isValid = TableConfigValidator.IsValid(config);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidSeatCount_ReturnsError(int seats)
    {
        // Arrange
        var config = CreateValidConfig() with { MaxSeats = seats };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().Contain(e => e.Contains("seats"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(10)]
    public void Validate_ValidSeatCount_ReturnsNoSeatError(int seats)
    {
        // Arrange
        var config = CreateValidConfig() with { MaxSeats = seats };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().NotContain(e => e.Contains("seats"));
    }

    [Fact]
    public void Validate_ZeroSmallBlind_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig() with { SmallBlind = 0 };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().Contain(e => e.Contains("Small blind") && e.Contains("greater than 0"));
    }

    [Fact]
    public void Validate_NegativeSmallBlind_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig() with { SmallBlind = -1 };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().Contain(e => e.Contains("Small blind") && e.Contains("greater than 0"));
    }

    [Fact]
    public void Validate_ZeroBigBlind_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig() with { BigBlind = 0 };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().Contain(e => e.Contains("Big blind") && e.Contains("greater than 0"));
    }

    [Fact]
    public void Validate_BigBlindLessThanSmallBlind_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig() with { SmallBlind = 10, BigBlind = 5 };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().Contain(e => e.Contains("Big blind") && e.Contains("greater than or equal"));
    }

    [Fact]
    public void Validate_ZeroMinBuyIn_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig() with { MinBuyIn = 0 };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().Contain(e => e.Contains("Minimum buy-in") && e.Contains("greater than 0"));
    }

    [Fact]
    public void Validate_NegativeMinBuyIn_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig() with { MinBuyIn = -10 };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().Contain(e => e.Contains("Minimum buy-in"));
    }

    [Fact]
    public void Validate_MaxBuyInLessThanMinBuyIn_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig() with { MinBuyIn = 200, MaxBuyIn = 100 };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().Contain(e => e.Contains("Maximum buy-in") && e.Contains("greater than or equal"));
    }

    [Fact]
    public void Validate_ZeroMaxBuyIn_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig() with { MaxBuyIn = 0 };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().Contain(e => e.Contains("Maximum buy-in") && e.Contains("greater than 0"));
    }

    [Fact]
    public void Validate_SevenCardStudWithMoreThan8Seats_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig() with { Variant = PokerVariant.SevenCardStud, MaxSeats = 9 };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().Contain(e => e.Contains("Seven Card Stud") && e.Contains("8 players"));
    }

    [Fact]
    public void Validate_SevenCardStudWith8Seats_ReturnsNoError()
    {
        // Arrange
        var config = CreateValidConfig() with { Variant = PokerVariant.SevenCardStud, MaxSeats = 8 };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().NotContain(e => e.Contains("Seven Card Stud"));
    }

    [Fact]
    public void Validate_NegativeAnte_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfig() with { Ante = -1 };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().Contain(e => e.Contains("Ante") && e.Contains("negative"));
    }

    [Fact]
    public void Validate_ZeroAnte_ReturnsNoError()
    {
        // Arrange
        var config = CreateValidConfig() with { Ante = 0 };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().NotContain(e => e.Contains("Ante"));
    }

    [Fact]
    public void Validate_PositiveAnte_ReturnsNoError()
    {
        // Arrange
        var config = CreateValidConfig() with { Ante = 1 };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().NotContain(e => e.Contains("Ante"));
    }

    [Theory]
    [InlineData(LimitType.NoLimit)]
    [InlineData(LimitType.FixedLimit)]
    [InlineData(LimitType.PotLimit)]
    [InlineData(LimitType.SpreadLimit)]
    public void Validate_AllLimitTypes_ReturnsNoError(LimitType limitType)
    {
        // Arrange
        var config = CreateValidConfig() with { LimitType = limitType };

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAndThrow_InvalidConfig_ThrowsValidationException()
    {
        // Arrange
        var config = CreateValidConfig() with { SmallBlind = 0 };

        // Act & Assert
        var act = () => TableConfigValidator.ValidateAndThrow(config);
        act.Should().Throw<TableConfigValidationException>()
            .Which.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateAndThrow_ValidConfig_DoesNotThrow()
    {
        // Arrange
        var config = CreateValidConfig();

        // Act & Assert
        var act = () => TableConfigValidator.ValidateAndThrow(config);
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_NullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => TableConfigValidator.Validate(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var config = new TableConfigDto(
            Variant: PokerVariant.TexasHoldem,
            MaxSeats: 1, // Invalid
            SmallBlind: 0, // Invalid
            BigBlind: 0, // Invalid
            MinBuyIn: 0); // Invalid

        // Act
        var errors = TableConfigValidator.Validate(config);

        // Assert
        errors.Count.Should().BeGreaterThanOrEqualTo(4);
    }

    private static TableConfigDto CreateValidConfig()
    {
        return new TableConfigDto(
            Variant: PokerVariant.TexasHoldem,
            MaxSeats: 6,
            SmallBlind: 1,
            BigBlind: 2,
            LimitType: LimitType.NoLimit,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            Ante: 0);
    }
}
