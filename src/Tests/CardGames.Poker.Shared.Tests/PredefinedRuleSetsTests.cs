using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.RuleSets;
using CardGames.Poker.Shared.Validation;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Shared.Tests;

public class PredefinedRuleSetsTests
{
    [Fact]
    public void TexasHoldem_IsValid()
    {
        // Act
        var isValid = RuleSetValidator.IsValid(PredefinedRuleSets.TexasHoldem);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void TexasHoldem_HasCorrectProperties()
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.TexasHoldem;

        // Assert
        ruleSet.Id.Should().Be("texas-holdem-nolimit");
        ruleSet.Variant.Should().Be(PokerVariant.TexasHoldem);
        ruleSet.HoleCardRules.Count.Should().Be(2);
        ruleSet.CommunityCardRules.Should().NotBeNull();
        ruleSet.CommunityCardRules!.TotalCount.Should().Be(5);
        ruleSet.BettingRounds.Should().HaveCount(4);
        ruleSet.LimitType.Should().Be(LimitType.NoLimit);
    }

    [Fact]
    public void TexasHoldem_HasCorrectBettingRounds()
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.TexasHoldem;

        // Assert
        ruleSet.BettingRounds[0].Name.Should().Be("Preflop");
        ruleSet.BettingRounds[0].CommunityCardsDealt.Should().Be(0);
        ruleSet.BettingRounds[0].HoleCardsDealt.Should().Be(2);

        ruleSet.BettingRounds[1].Name.Should().Be("Flop");
        ruleSet.BettingRounds[1].CommunityCardsDealt.Should().Be(3);

        ruleSet.BettingRounds[2].Name.Should().Be("Turn");
        ruleSet.BettingRounds[2].CommunityCardsDealt.Should().Be(1);

        ruleSet.BettingRounds[3].Name.Should().Be("River");
        ruleSet.BettingRounds[3].CommunityCardsDealt.Should().Be(1);
    }

    [Fact]
    public void Omaha_IsValid()
    {
        // Act
        var isValid = RuleSetValidator.IsValid(PredefinedRuleSets.Omaha);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Omaha_HasCorrectProperties()
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.Omaha;

        // Assert
        ruleSet.Id.Should().Be("omaha-potlimit");
        ruleSet.Variant.Should().Be(PokerVariant.Omaha);
        ruleSet.HoleCardRules.Count.Should().Be(4);
        ruleSet.HoleCardRules.MinUsedInHand.Should().Be(2);
        ruleSet.HoleCardRules.MaxUsedInHand.Should().Be(2);
        ruleSet.CommunityCardRules.Should().NotBeNull();
        ruleSet.CommunityCardRules!.TotalCount.Should().Be(5);
        ruleSet.CommunityCardRules.MinUsedInHand.Should().Be(3);
        ruleSet.CommunityCardRules.MaxUsedInHand.Should().Be(3);
        ruleSet.BettingRounds.Should().HaveCount(4);
        ruleSet.LimitType.Should().Be(LimitType.PotLimit);
    }

    [Fact]
    public void Omaha_HasCorrectBettingRounds()
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.Omaha;

        // Assert
        ruleSet.BettingRounds[0].Name.Should().Be("Preflop");
        ruleSet.BettingRounds[0].HoleCardsDealt.Should().Be(4);
        
        ruleSet.BettingRounds[1].Name.Should().Be("Flop");
        ruleSet.BettingRounds[1].CommunityCardsDealt.Should().Be(3);
    }

    [Fact]
    public void All_ContainsBothVariants()
    {
        // Assert
        PredefinedRuleSets.All.Should().ContainKey(PokerVariant.TexasHoldem);
        PredefinedRuleSets.All.Should().ContainKey(PokerVariant.Omaha);
        PredefinedRuleSets.All.Should().HaveCount(2);
    }

    [Fact]
    public void GetByVariant_TexasHoldem_ReturnsCorrectRuleSet()
    {
        // Act
        var ruleSet = PredefinedRuleSets.GetByVariant(PokerVariant.TexasHoldem);

        // Assert
        ruleSet.Should().NotBeNull();
        ruleSet.Should().Be(PredefinedRuleSets.TexasHoldem);
    }

    [Fact]
    public void GetByVariant_Omaha_ReturnsCorrectRuleSet()
    {
        // Act
        var ruleSet = PredefinedRuleSets.GetByVariant(PokerVariant.Omaha);

        // Assert
        ruleSet.Should().NotBeNull();
        ruleSet.Should().Be(PredefinedRuleSets.Omaha);
    }

    [Fact]
    public void GetByVariant_UnknownVariant_ReturnsNull()
    {
        // Act
        var ruleSet = PredefinedRuleSets.GetByVariant(PokerVariant.SevenCardStud);

        // Assert
        ruleSet.Should().BeNull();
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem)]
    [InlineData(PokerVariant.Omaha)]
    public void AllPredefinedRuleSets_HaveValidSchemaVersion(PokerVariant variant)
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.GetByVariant(variant);

        // Assert
        ruleSet.Should().NotBeNull();
        ruleSet!.SchemaVersion.Should().Be(RuleSetValidator.CurrentSchemaVersion);
    }

    [Theory]
    [InlineData(PokerVariant.TexasHoldem)]
    [InlineData(PokerVariant.Omaha)]
    public void AllPredefinedRuleSets_UseStandardDeck(PokerVariant variant)
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.GetByVariant(variant);

        // Assert
        ruleSet.Should().NotBeNull();
        ruleSet!.DeckComposition.DeckType.Should().Be(DeckType.Full52);
        ruleSet.DeckComposition.NumberOfDecks.Should().Be(1);
    }
}
