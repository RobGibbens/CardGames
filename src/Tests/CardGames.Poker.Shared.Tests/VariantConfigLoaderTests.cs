using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.RuleSets;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Shared.Tests;

public class VariantConfigLoaderTests
{
    [Fact]
    public void ForVariant_TexasHoldem_ReturnsValidConfig()
    {
        // Act
        var config = VariantConfigLoader.ForVariant(PokerVariant.TexasHoldem);

        // Assert
        config.Should().NotBeNull();
        config!.Variant.Should().Be(PokerVariant.TexasHoldem);
        config.VariantName.Should().Be("Texas Hold'em (No Limit)");
    }

    [Fact]
    public void ForVariant_UnknownVariant_ReturnsNull()
    {
        // Act
        var config = VariantConfigLoader.ForVariant(PokerVariant.DealersChoice);

        // Assert
        config.Should().BeNull();
    }

    [Fact]
    public void FromRuleSet_ValidRuleSet_ReturnsConfig()
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.Omaha;

        // Act
        var config = VariantConfigLoader.FromRuleSet(ruleSet);

        // Assert
        config.Should().NotBeNull();
        config.Variant.Should().Be(PokerVariant.Omaha);
    }

    #region Card Configuration Tests

    [Fact]
    public void TexasHoldem_HasCorrectCardConfiguration()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.TexasHoldem)!;

        // Assert
        config.HoleCardCount.Should().Be(2);
        config.MinHoleCardsUsed.Should().Be(0);
        config.MaxHoleCardsUsed.Should().Be(2);
        config.HasCommunityCards.Should().BeTrue();
        config.CommunityCardCount.Should().Be(5);
    }

    [Fact]
    public void Omaha_HasCorrectCardConfiguration()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.Omaha)!;

        // Assert
        config.HoleCardCount.Should().Be(4);
        config.MinHoleCardsUsed.Should().Be(2);
        config.MaxHoleCardsUsed.Should().Be(2);
        config.MinCommunityCardsUsed.Should().Be(3);
        config.MaxCommunityCardsUsed.Should().Be(3);
    }

    [Fact]
    public void SevenCardStud_HasCorrectCardConfiguration()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud)!;

        // Assert
        config.HoleCardCount.Should().Be(7);
        config.HasCommunityCards.Should().BeFalse();
        config.IsStudGame.Should().BeTrue();
    }

    [Fact]
    public void FiveCardDraw_HasCorrectCardConfiguration()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.FiveCardDraw)!;

        // Assert
        config.HoleCardCount.Should().Be(5);
        config.HasCommunityCards.Should().BeFalse();
        config.AllowsDraw.Should().BeTrue();
        config.MaxDrawCount.Should().Be(3);
    }

    #endregion

    #region Stud Game Configuration Tests

    [Fact]
    public void SevenCardStud_HasCorrectStudConfiguration()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud)!;

        // Assert
        config.IsStudGame.Should().BeTrue();
        config.FaceUpCardIndices.Should().BeEquivalentTo(new[] { 2, 3, 4, 5 });
        config.FaceDownCardIndices.Should().BeEquivalentTo(new[] { 0, 1, 6 });
        config.OpponentFaceUpCardCount.Should().Be(4);
        config.OpponentFaceDownCardCount.Should().Be(3);
    }

    [Fact]
    public void SevenCardStud_IsCardFaceUp_ReturnsCorrectValues()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud)!;

        // Assert
        config.IsCardFaceUp(0).Should().BeFalse(); // First hole card
        config.IsCardFaceUp(1).Should().BeFalse(); // Second hole card
        config.IsCardFaceUp(2).Should().BeTrue();  // First upcard
        config.IsCardFaceUp(3).Should().BeTrue();  // Second upcard
        config.IsCardFaceUp(4).Should().BeTrue();  // Third upcard
        config.IsCardFaceUp(5).Should().BeTrue();  // Fourth upcard
        config.IsCardFaceUp(6).Should().BeFalse(); // River (down card)
    }

    [Fact]
    public void TexasHoldem_IsNotStudGame()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.TexasHoldem)!;

        // Assert
        config.IsStudGame.Should().BeFalse();
        config.FaceUpCardIndices.Should().BeEmpty();
    }

    #endregion

    #region Betting Configuration Tests

    [Fact]
    public void TexasHoldem_HasCorrectBettingConfiguration()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.TexasHoldem)!;

        // Assert
        config.LimitType.Should().Be(LimitType.NoLimit);
        config.HasAnte.Should().BeFalse();
        config.HasSmallBlind.Should().BeTrue();
        config.HasBigBlind.Should().BeTrue();
        config.AllowStraddle.Should().BeTrue();
    }

    [Fact]
    public void Omaha_HasCorrectLimitType()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.Omaha)!;

        // Assert
        config.LimitType.Should().Be(LimitType.PotLimit);
    }

    [Fact]
    public void SevenCardStud_HasCorrectBettingConfiguration()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud)!;

        // Assert
        config.LimitType.Should().Be(LimitType.FixedLimit);
        config.HasAnte.Should().BeTrue();
        config.HasSmallBlind.Should().BeFalse();
        config.HasBigBlind.Should().BeFalse();
        config.HasBringIn.Should().BeTrue();
    }

    [Fact]
    public void BettingRounds_HaveCorrectNames()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.TexasHoldem)!;

        // Assert
        config.NumberOfBettingRounds.Should().Be(4);
        config.GetBettingRoundName(0).Should().Be("Preflop");
        config.GetBettingRoundName(1).Should().Be("Flop");
        config.GetBettingRoundName(2).Should().Be("Turn");
        config.GetBettingRoundName(3).Should().Be("River");
    }

    [Fact]
    public void FixedLimit_UsesBigBet_OnLaterStreets()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud)!;

        // Assert - Third/Fourth street use small bet (0.5 multiplier)
        config.UsesBigBet(0).Should().BeFalse(); // Third Street
        config.UsesBigBet(1).Should().BeFalse(); // Fourth Street
        
        // Fifth/Sixth/Seventh street use big bet (1.0 multiplier)
        config.UsesBigBet(2).Should().BeTrue();  // Fifth Street
        config.UsesBigBet(3).Should().BeTrue();  // Sixth Street
        config.UsesBigBet(4).Should().BeTrue();  // Seventh Street
    }

    #endregion

    #region Hi/Lo Configuration Tests

    [Fact]
    public void TexasHoldem_IsNotHiLoGame()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.TexasHoldem)!;

        // Assert
        config.IsHiLoGame.Should().BeFalse();
    }

    #endregion

    #region Wildcard Configuration Tests

    [Fact]
    public void TexasHoldem_HasNoWildcards()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.TexasHoldem)!;

        // Assert
        config.HasWildcards.Should().BeFalse();
        config.WildcardCards.Should().BeEmpty();
    }

    [Fact]
    public void Baseball_HasWildcards()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.Baseball)!;

        // Assert
        config.HasWildcards.Should().BeTrue();
        config.WildcardCards.Should().Contain("3h");
        config.WildcardCards.Should().Contain("9h");
    }

    [Fact]
    public void FollowTheQueen_HasDynamicWildcards()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.FollowTheQueen)!;

        // Assert
        config.HasWildcards.Should().BeTrue();
        config.HasDynamicWildcards.Should().BeTrue();
        config.DynamicWildcardRule.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void KingsAndLows_HasDynamicWildcards()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.KingsAndLows)!;

        // Assert
        config.HasWildcards.Should().BeTrue();
        config.HasDynamicWildcards.Should().BeTrue();
        config.WildcardCards.Should().Contain("Kh");
    }

    #endregion

    #region Special Rules Tests

    [Fact]
    public void FiveCardDraw_HasDrawPhase()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.FiveCardDraw)!;

        // Assert
        config.HasDrawPhase.Should().BeTrue();
        config.IsSpecialRuleEnabled("draw-phase").Should().BeTrue();
    }

    [Fact]
    public void KingsAndLows_HasDropOrStay()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.KingsAndLows)!;

        // Assert
        config.HasDropOrStay.Should().BeTrue();
        config.HasLosersMatchPot.Should().BeTrue();
    }

    #endregion

    #region Display Type Tests

    [Fact]
    public void TexasHoldem_DisplayType_IsCommunityCards()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.TexasHoldem)!;

        // Assert
        config.DisplayType.Should().Be(VariantDisplayType.CommunityCards);
    }

    [Fact]
    public void SevenCardStud_DisplayType_IsStud()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud)!;

        // Assert
        config.DisplayType.Should().Be(VariantDisplayType.Stud);
    }

    [Fact]
    public void FiveCardDraw_DisplayType_IsDraw()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.FiveCardDraw)!;

        // Assert
        config.DisplayType.Should().Be(VariantDisplayType.Draw);
    }

    #endregion

    #region UI Configuration Tests

    [Fact]
    public void TexasHoldem_RecommendedCardSize_IsLarge()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.TexasHoldem)!;

        // Assert
        config.RecommendedCardSize.Should().Be("large");
    }

    [Fact]
    public void Omaha_RecommendedCardSize_IsMedium()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.Omaha)!;

        // Assert
        config.RecommendedCardSize.Should().Be("medium");
    }

    [Fact]
    public void SevenCardStud_RecommendedCardSize_IsSmall()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud)!;

        // Assert
        config.RecommendedCardSize.Should().Be("small");
    }

    [Fact]
    public void SevenCardStud_RecommendedOpponentLayout_IsStud()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud)!;

        // Assert
        config.RecommendedOpponentLayout.Should().Be("stud");
    }

    [Fact]
    public void Omaha_RecommendedOpponentLayout_IsStacked()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.Omaha)!;

        // Assert
        config.RecommendedOpponentLayout.Should().Be("stacked");
    }

    #endregion

    #region ToDisplayConfig Tests

    [Fact]
    public void ToDisplayConfig_ReturnsValidDto()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.TexasHoldem)!;

        // Act
        var displayConfig = config.ToDisplayConfig();

        // Assert
        displayConfig.Should().NotBeNull();
        displayConfig.Variant.Should().Be(PokerVariant.TexasHoldem);
        displayConfig.Name.Should().Be("Texas Hold'em (No Limit)");
        displayConfig.LimitType.Should().Be(LimitType.NoLimit);
        displayConfig.LimitTypeName.Should().Be("No Limit");
        displayConfig.LimitTypeAbbreviation.Should().Be("NL");
        displayConfig.HoleCardCount.Should().Be(2);
        displayConfig.HasCommunityCards.Should().BeTrue();
        displayConfig.CommunityCardCount.Should().Be(5);
        displayConfig.DisplayType.Should().Be(VariantDisplayType.CommunityCards);
    }

    [Fact]
    public void ToDisplayConfig_StudGame_HasCorrectFaceUpIndices()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.SevenCardStud)!;

        // Act
        var displayConfig = config.ToDisplayConfig();

        // Assert
        displayConfig.IsStudGame.Should().BeTrue();
        displayConfig.FaceUpCardIndices.Should().BeEquivalentTo(new[] { 2, 3, 4, 5 });
        displayConfig.FaceDownCardIndices.Should().BeEquivalentTo(new[] { 0, 1, 6 });
    }

    [Fact]
    public void ToDisplayConfig_DrawGame_HasDrawConfiguration()
    {
        // Arrange
        var config = VariantConfigLoader.ForVariant(PokerVariant.FiveCardDraw)!;

        // Act
        var displayConfig = config.ToDisplayConfig();

        // Assert
        displayConfig.AllowsDraw.Should().BeTrue();
        displayConfig.MaxDrawCount.Should().Be(3);
        displayConfig.DisplayType.Should().Be(VariantDisplayType.Draw);
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem)]
    [InlineData(PokerVariant.Omaha)]
    [InlineData(PokerVariant.SevenCardStud)]
    [InlineData(PokerVariant.FiveCardDraw)]
    [InlineData(PokerVariant.Baseball)]
    [InlineData(PokerVariant.FollowTheQueen)]
    [InlineData(PokerVariant.KingsAndLows)]
    public void ToDisplayConfig_AllVariants_ReturnValidDto(PokerVariant variant)
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
        displayConfig.BettingRoundNames.Should().NotBeEmpty();
    }

    #endregion
}
