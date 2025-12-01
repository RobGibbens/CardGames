using System;
using CardGames.Poker.Betting;
using CardGames.Poker.Shared.RuleSets;
using FluentAssertions;
using Xunit;
using SharedLimitType = CardGames.Poker.Shared.Enums.LimitType;
using PokerVariant = CardGames.Poker.Shared.Enums.PokerVariant;

namespace CardGames.Poker.Tests.Betting;

public class VariantLimitStrategyFactoryTests
{
    [Theory]
    [InlineData(PokerVariant.TexasHoldem, typeof(NoLimitStrategy))]
    [InlineData(PokerVariant.Omaha, typeof(PotLimitStrategy))]
    [InlineData(PokerVariant.SevenCardStud, typeof(FixedLimitStrategy))]
    [InlineData(PokerVariant.FiveCardDraw, typeof(FixedLimitStrategy))]
    public void CreateFromConfig_ReturnsCorrectStrategyType(PokerVariant variant, Type expectedType)
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(variant)!;

        // Act
        var strategy = VariantLimitStrategyFactory.CreateFromConfig(config);

        // Assert
        strategy.Should().BeOfType(expectedType);
    }

    [Fact]
    public void CreateFromConfig_TexasHoldem_ReturnsNoLimitStrategy()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.TexasHoldem)!;

        // Act
        var strategy = VariantLimitStrategyFactory.CreateFromConfig(config);

        // Assert
        strategy.Should().BeOfType<NoLimitStrategy>();
    }

    [Fact]
    public void CreateFromConfig_Omaha_ReturnsPotLimitStrategy()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.Omaha)!;

        // Act
        var strategy = VariantLimitStrategyFactory.CreateFromConfig(config);

        // Assert
        strategy.Should().BeOfType<PotLimitStrategy>();
    }

    [Fact]
    public void CreateFromConfig_SevenCardStud_ReturnsFixedLimitStrategy()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud)!;

        // Act
        var strategy = VariantLimitStrategyFactory.CreateFromConfig(
            config,
            smallBet: 10,
            bigBet: 20,
            bettingRoundOrder: 0);

        // Assert
        strategy.Should().BeOfType<FixedLimitStrategy>();
    }

    [Fact]
    public void CreateFromRuleSet_ValidRuleSet_ReturnsStrategy()
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.TexasHoldem;

        // Act
        var strategy = VariantLimitStrategyFactory.CreateFromRuleSet(ruleSet);

        // Assert
        strategy.Should().NotBeNull();
        strategy.Should().BeOfType<NoLimitStrategy>();
    }

    [Fact]
    public void CreateForBettingRound_ReturnsCorrectStrategy()
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.SevenCardStud;

        // Act
        var strategy = VariantLimitStrategyFactory.CreateForBettingRound(
            ruleSet,
            bettingRoundOrder: 0,
            bigBlind: 20);

        // Assert
        strategy.Should().BeOfType<FixedLimitStrategy>();
    }

    [Theory]
    [InlineData(SharedLimitType.NoLimit, "No Limit")]
    [InlineData(SharedLimitType.PotLimit, "Pot Limit")]
    [InlineData(SharedLimitType.FixedLimit, "Fixed Limit")]
    [InlineData(SharedLimitType.SpreadLimit, "Spread Limit")]
    public void GetLimitTypeDisplayName_ReturnsCorrectName(SharedLimitType limitType, string expectedName)
    {
        // Act
        var name = VariantLimitStrategyFactory.GetLimitTypeDisplayName(limitType);

        // Assert
        name.Should().Be(expectedName);
    }

    [Theory]
    [InlineData(SharedLimitType.NoLimit, "NL")]
    [InlineData(SharedLimitType.PotLimit, "PL")]
    [InlineData(SharedLimitType.FixedLimit, "FL")]
    [InlineData(SharedLimitType.SpreadLimit, "SL")]
    public void GetLimitTypeAbbreviation_ReturnsCorrectAbbreviation(SharedLimitType limitType, string expectedAbbr)
    {
        // Act
        var abbr = VariantLimitStrategyFactory.GetLimitTypeAbbreviation(limitType);

        // Assert
        abbr.Should().Be(expectedAbbr);
    }

    [Fact]
    public void CreateFromLimitType_NoLimit_ReturnsNoLimitStrategy()
    {
        // Act
        var strategy = VariantLimitStrategyFactory.CreateFromLimitType(SharedLimitType.NoLimit);

        // Assert
        strategy.Should().BeOfType<NoLimitStrategy>();
    }

    [Fact]
    public void CreateFromLimitType_PotLimit_ReturnsPotLimitStrategy()
    {
        // Act
        var strategy = VariantLimitStrategyFactory.CreateFromLimitType(SharedLimitType.PotLimit);

        // Assert
        strategy.Should().BeOfType<PotLimitStrategy>();
    }

    [Fact]
    public void CreateFromLimitType_FixedLimit_ReturnsFixedLimitStrategy()
    {
        // Act
        var strategy = VariantLimitStrategyFactory.CreateFromLimitType(
            SharedLimitType.FixedLimit,
            smallBet: 10,
            bigBet: 20);

        // Assert
        strategy.Should().BeOfType<FixedLimitStrategy>();
    }

    [Fact]
    public void CreateFromLimitType_SpreadLimit_FallsBackToNoLimit()
    {
        // Act
        var strategy = VariantLimitStrategyFactory.CreateFromLimitType(SharedLimitType.SpreadLimit);

        // Assert
        strategy.Should().BeOfType<NoLimitStrategy>();
    }

    [Fact]
    public void NoLimitStrategy_AllowsAllInBet()
    {
        // Arrange
        var strategy = VariantLimitStrategyFactory.CreateFromLimitType(SharedLimitType.NoLimit);

        // Act & Assert
        strategy.GetMaxBet(1000, 100, 0, 0).Should().Be(1000);
    }

    [Fact]
    public void PotLimitStrategy_LimitsBetToPotSize()
    {
        // Arrange
        var strategy = VariantLimitStrategyFactory.CreateFromLimitType(SharedLimitType.PotLimit);

        // Act
        var maxBet = strategy.GetMaxBet(1000, 100, 0, 0);

        // Assert
        maxBet.Should().Be(100); // Limited to pot size
    }

    [Fact]
    public void FixedLimitStrategy_UsesSmallBetByDefault()
    {
        // Arrange
        var strategy = (FixedLimitStrategy)VariantLimitStrategyFactory.CreateFromLimitType(
            SharedLimitType.FixedLimit,
            smallBet: 10,
            bigBet: 20,
            useBigBet: false);

        // Assert
        strategy.BettingIncrement.Should().Be(10);
    }

    [Fact]
    public void FixedLimitStrategy_UsesBigBetWhenSpecified()
    {
        // Arrange
        var strategy = (FixedLimitStrategy)VariantLimitStrategyFactory.CreateFromLimitType(
            SharedLimitType.FixedLimit,
            smallBet: 10,
            bigBet: 20,
            useBigBet: true);

        // Assert
        strategy.BettingIncrement.Should().Be(20);
    }

    [Fact]
    public void CreateFromConfig_FixedLimit_EarlyStreet_UsesSmallBet()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud)!;

        // Act - Third street uses small bet (0.5 multiplier)
        var strategy = (FixedLimitStrategy)VariantLimitStrategyFactory.CreateFromConfig(
            config,
            smallBet: 10,
            bigBet: 20,
            bettingRoundOrder: 0);

        // Assert
        strategy.BettingIncrement.Should().Be(10);
    }

    [Fact]
    public void CreateFromConfig_FixedLimit_LateStreet_UsesBigBet()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud)!;

        // Act - Fifth street uses big bet (1.0 multiplier)
        var strategy = (FixedLimitStrategy)VariantLimitStrategyFactory.CreateFromConfig(
            config,
            smallBet: 10,
            bigBet: 20,
            bettingRoundOrder: 2);

        // Assert
        strategy.BettingIncrement.Should().Be(20);
    }
}
