using CardGames.Poker.Betting;
using CardGames.Poker.Games;
using CardGames.Poker.Shared.DTOs.RuleSets;
using CardGames.Poker.Shared.RuleSets;
using FluentAssertions;
using Xunit;
using PokerVariant = CardGames.Poker.Shared.Enums.PokerVariant;
using SharedLimitType = CardGames.Poker.Shared.Enums.LimitType;

namespace CardGames.Poker.Tests.Variants;

/// <summary>
/// Integration tests for variant-specific adaptations.
/// Tests that each variant can be configured correctly with the right rules.
/// </summary>
public class VariantIntegrationTests
{
    #region Texas Hold'em Tests

    [Fact]
    public void TexasHoldem_ConfigLoadsCorrectly()
    {
        // Arrange & Act
        var config = VariantConfigLoader.ForVariant(PokerVariant.TexasHoldem);

        // Assert
        config.Should().NotBeNull();
        config!.Variant.Should().Be(PokerVariant.TexasHoldem);
        config.HoleCardCount.Should().Be(2);
        config.CommunityCardCount.Should().Be(5);
        config.LimitType.Should().Be(SharedLimitType.NoLimit);
        config.HasCommunityCards.Should().BeTrue();
        config.IsStudGame.Should().BeFalse();
        config.AllowsDraw.Should().BeFalse();
    }

    [Fact]
    public void TexasHoldem_GameCanBeCreated()
    {
        // Arrange
        var players = new[] { ("Alice", 1000), ("Bob", 1000), ("Charlie", 1000) };

        // Act
        var game = new HoldEmGame(players, 5, 10);

        // Assert
        game.Should().NotBeNull();
        game.SmallBlind.Should().Be(5);
        game.BigBlind.Should().Be(10);
        game.Players.Should().HaveCount(3);
    }

    [Fact]
    public void TexasHoldem_BettingStrategyIsNoLimit()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.TexasHoldem)!;

        // Act
        var strategy = VariantLimitStrategyFactory.CreateFromConfig(config);

        // Assert
        strategy.Should().BeOfType<NoLimitStrategy>();
        strategy.GetMaxBet(1000, 100, 0, 0).Should().Be(1000); // Can go all-in
    }

    #endregion

    #region Omaha Tests

    [Fact]
    public void Omaha_ConfigLoadsCorrectly()
    {
        // Arrange & Act
        var config = VariantConfigLoader.ForVariant(PokerVariant.Omaha);

        // Assert
        config.Should().NotBeNull();
        config!.Variant.Should().Be(PokerVariant.Omaha);
        config.HoleCardCount.Should().Be(4);
        config.MinHoleCardsUsed.Should().Be(2);
        config.MaxHoleCardsUsed.Should().Be(2);
        config.CommunityCardCount.Should().Be(5);
        config.MinCommunityCardsUsed.Should().Be(3);
        config.MaxCommunityCardsUsed.Should().Be(3);
        config.LimitType.Should().Be(SharedLimitType.PotLimit);
    }

    [Fact]
    public void Omaha_GameCanBeCreated()
    {
        // Arrange
        var players = new[] { ("Alice", 1000), ("Bob", 1000), ("Charlie", 1000) };

        // Act
        var game = new OmahaGame(players, 5, 10);

        // Assert
        game.Should().NotBeNull();
        game.SmallBlind.Should().Be(5);
        game.BigBlind.Should().Be(10);
        game.Players.Should().HaveCount(3);
    }

    [Fact]
    public void Omaha_BettingStrategyIsPotLimit()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.Omaha)!;

        // Act
        var strategy = VariantLimitStrategyFactory.CreateFromConfig(config);

        // Assert
        strategy.Should().BeOfType<PotLimitStrategy>();
        strategy.GetMaxBet(1000, 100, 0, 0).Should().Be(100); // Limited to pot size
    }

    #endregion

    #region Seven Card Stud Tests

    [Fact]
    public void SevenCardStud_ConfigLoadsCorrectly()
    {
        // Arrange & Act
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud);

        // Assert
        config.Should().NotBeNull();
        config!.Variant.Should().Be(PokerVariant.SevenCardStud);
        config.HoleCardCount.Should().Be(7);
        config.HasCommunityCards.Should().BeFalse();
        config.IsStudGame.Should().BeTrue();
        config.FaceUpCardIndices.Should().BeEquivalentTo(new[] { 2, 3, 4, 5 });
        config.FaceDownCardIndices.Should().BeEquivalentTo(new[] { 0, 1, 6 });
        config.LimitType.Should().Be(SharedLimitType.FixedLimit);
        config.HasAnte.Should().BeTrue();
        config.HasBringIn.Should().BeTrue();
    }

    [Fact]
    public void SevenCardStud_GameCanBeCreated()
    {
        // Arrange
        var players = new[] { ("Alice", 1000), ("Bob", 1000), ("Charlie", 1000) };

        // Act
        var game = new SevenCardStudGame(players, 5, 10, 10, 20, useBringIn: true);

        // Assert
        game.Should().NotBeNull();
        game.Ante.Should().Be(5);
        game.BringIn.Should().Be(10);
        game.SmallBet.Should().Be(10);
        game.BigBet.Should().Be(20);
    }

    [Fact]
    public void SevenCardStud_BettingStrategyIsFixedLimit()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud)!;

        // Act - Early street
        var strategyEarly = VariantLimitStrategyFactory.CreateFromConfig(config, 10, 20, 0);
        
        // Act - Late street
        var strategyLate = VariantLimitStrategyFactory.CreateFromConfig(config, 10, 20, 2);

        // Assert
        strategyEarly.Should().BeOfType<FixedLimitStrategy>();
        ((FixedLimitStrategy)strategyEarly).BettingIncrement.Should().Be(10); // Small bet
        ((FixedLimitStrategy)strategyLate).BettingIncrement.Should().Be(20); // Big bet
    }

    #endregion

    #region Five Card Draw Tests

    [Fact]
    public void FiveCardDraw_ConfigLoadsCorrectly()
    {
        // Arrange & Act
        var config = VariantConfigLoader.ForVariant(PokerVariant.FiveCardDraw);

        // Assert
        config.Should().NotBeNull();
        config!.Variant.Should().Be(PokerVariant.FiveCardDraw);
        config.HoleCardCount.Should().Be(5);
        config.HasCommunityCards.Should().BeFalse();
        config.IsStudGame.Should().BeFalse();
        config.AllowsDraw.Should().BeTrue();
        config.MaxDrawCount.Should().Be(3);
        config.HasDrawPhase.Should().BeTrue();
        config.LimitType.Should().Be(SharedLimitType.FixedLimit);
    }

    [Fact]
    public void FiveCardDraw_GameCanBeCreated()
    {
        // Arrange
        var players = new[] { ("Alice", 1000), ("Bob", 1000), ("Charlie", 1000) };

        // Act
        var game = new FiveCardDrawGame(players, 5, 10);

        // Assert
        game.Should().NotBeNull();
        game.Ante.Should().Be(5);
        game.Players.Should().HaveCount(3);
    }

    #endregion

    #region Baseball Tests

    [Fact]
    public void Baseball_ConfigLoadsCorrectly()
    {
        // Arrange & Act
        var config = VariantConfigLoader.ForVariant(PokerVariant.Baseball);

        // Assert
        config.Should().NotBeNull();
        config!.Variant.Should().Be(PokerVariant.Baseball);
        config.IsStudGame.Should().BeTrue();
        config.HasWildcards.Should().BeTrue();
        config.WildcardCards.Should().Contain("3h");
        config.WildcardCards.Should().Contain("9h");
    }

    #endregion

    #region Follow The Queen Tests

    [Fact]
    public void FollowTheQueen_ConfigLoadsCorrectly()
    {
        // Arrange & Act
        var config = VariantConfigLoader.ForVariant(PokerVariant.FollowTheQueen);

        // Assert
        config.Should().NotBeNull();
        config!.Variant.Should().Be(PokerVariant.FollowTheQueen);
        config.IsStudGame.Should().BeTrue();
        config.HasWildcards.Should().BeTrue();
        config.HasDynamicWildcards.Should().BeTrue();
        config.DynamicWildcardRule.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Kings and Lows Tests

    [Fact]
    public void KingsAndLows_ConfigLoadsCorrectly()
    {
        // Arrange & Act
        var config = VariantConfigLoader.ForVariant(PokerVariant.KingsAndLows);

        // Assert
        config.Should().NotBeNull();
        config!.Variant.Should().Be(PokerVariant.KingsAndLows);
        config.HoleCardCount.Should().Be(5);
        config.AllowsDraw.Should().BeTrue();
        config.HasWildcards.Should().BeTrue();
        config.HasDynamicWildcards.Should().BeTrue();
        config.HasDropOrStay.Should().BeTrue();
        config.HasLosersMatchPot.Should().BeTrue();
    }

    #endregion

    #region Display Config Tests

    [Theory]
    [InlineData(PokerVariant.TexasHoldem)]
    [InlineData(PokerVariant.Omaha)]
    [InlineData(PokerVariant.SevenCardStud)]
    [InlineData(PokerVariant.FiveCardDraw)]
    [InlineData(PokerVariant.Baseball)]
    [InlineData(PokerVariant.FollowTheQueen)]
    [InlineData(PokerVariant.KingsAndLows)]
    public void AllVariants_CanGenerateDisplayConfig(PokerVariant variant)
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(variant);
        config.Should().NotBeNull();

        // Act
        var displayConfig = config!.ToDisplayConfig();

        // Assert
        displayConfig.Should().NotBeNull();
        displayConfig.Variant.Should().Be(variant);
        displayConfig.Name.Should().NotBeNullOrEmpty();
        displayConfig.HoleCardCount.Should().BeGreaterThan(0);
        displayConfig.BettingRoundNames.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem)]
    [InlineData(PokerVariant.Omaha)]
    [InlineData(PokerVariant.SevenCardStud)]
    [InlineData(PokerVariant.FiveCardDraw)]
    [InlineData(PokerVariant.Baseball)]
    [InlineData(PokerVariant.FollowTheQueen)]
    [InlineData(PokerVariant.KingsAndLows)]
    public void AllVariants_HaveValidLimitStrategy(PokerVariant variant)
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(variant);
        config.Should().NotBeNull();

        // Act
        var strategy = VariantLimitStrategyFactory.CreateFromConfig(config!);

        // Assert
        strategy.Should().NotBeNull();
        strategy.Should().BeAssignableTo<ILimitStrategy>();
    }

    #endregion

    #region Ruleset Validation Tests

    [Theory]
    [InlineData(PokerVariant.TexasHoldem)]
    [InlineData(PokerVariant.Omaha)]
    [InlineData(PokerVariant.SevenCardStud)]
    [InlineData(PokerVariant.FiveCardDraw)]
    [InlineData(PokerVariant.Baseball)]
    [InlineData(PokerVariant.FollowTheQueen)]
    [InlineData(PokerVariant.KingsAndLows)]
    public void AllPredefinedRulesets_AreValid(PokerVariant variant)
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.GetByVariant(variant);

        // Assert
        ruleSet.Should().NotBeNull();
        ruleSet!.Variant.Should().Be(variant);
        ruleSet.BettingRounds.Should().NotBeEmpty();
        ruleSet.HoleCardRules.Count.Should().BeGreaterThan(0);
    }

    #endregion
}
