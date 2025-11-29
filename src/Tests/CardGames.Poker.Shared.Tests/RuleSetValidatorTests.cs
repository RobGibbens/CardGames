using CardGames.Poker.Shared.DTOs.RuleSets;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.RuleSets;
using CardGames.Poker.Shared.Validation;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Shared.Tests;

public class RuleSetValidatorTests
{
    [Fact]
    public void Validate_ValidTexasHoldem_ReturnsNoErrors()
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.TexasHoldem;

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidOmaha_ReturnsNoErrors()
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.Omaha;

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_ValidRuleSet_ReturnsTrue()
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.TexasHoldem;

        // Act
        var isValid = RuleSetValidator.IsValid(ruleSet);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingSchemaVersion_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with { SchemaVersion = "" };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("SchemaVersion"));
    }

    [Fact]
    public void Validate_UnsupportedSchemaVersion_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with { SchemaVersion = "2.0" };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("SchemaVersion") && e.Contains("not supported"));
    }

    [Fact]
    public void Validate_MissingId_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with { Id = "" };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("Id"));
    }

    [Fact]
    public void Validate_MissingName_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with { Name = "" };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("Name"));
    }

    [Fact]
    public void Validate_ZeroDecks_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with
        {
            DeckComposition = new DeckCompositionDto(DeckType.Full52, NumberOfDecks: 0)
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("NumberOfDecks"));
    }

    [Fact]
    public void Validate_CustomDeckWithoutIncludedCards_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with
        {
            DeckComposition = new DeckCompositionDto(DeckType.Custom, NumberOfDecks: 1)
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("Custom deck") && e.Contains("IncludedCards"));
    }

    [Fact]
    public void Validate_NoBettingRounds_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with
        {
            BettingRounds = Array.Empty<BettingRoundDto>()
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("betting round"));
    }

    [Fact]
    public void Validate_DuplicateBettingRoundOrders_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with
        {
            BettingRounds = new List<BettingRoundDto>
            {
                new("Round1", 0),
                new("Round2", 0) // Duplicate order
            }
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("unique"));
    }

    [Fact]
    public void Validate_NonSequentialBettingRoundOrders_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with
        {
            BettingRounds = new List<BettingRoundDto>
            {
                new("Round1", 0),
                new("Round2", 2) // Should be 1
            }
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("sequential"));
    }

    [Fact]
    public void Validate_BettingRoundWithEmptyName_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with
        {
            BettingRounds = new List<BettingRoundDto>
            {
                new("", 0)
            }
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("name"));
    }

    [Fact]
    public void Validate_ZeroHoleCards_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with
        {
            HoleCardRules = new HoleCardRulesDto(Count: 0)
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("HoleCardRules.Count"));
    }

    [Fact]
    public void Validate_MaxUsedLessThanMin_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with
        {
            HoleCardRules = new HoleCardRulesDto(Count: 4, MinUsedInHand: 3, MaxUsedInHand: 2)
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("MaxUsedInHand") && e.Contains("less than"));
    }

    [Fact]
    public void Validate_AllowDrawWithZeroMaxDraw_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with
        {
            HoleCardRules = new HoleCardRulesDto(Count: 5, AllowDraw: true, MaxDrawCount: 0)
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("AllowDraw") && e.Contains("MaxDrawCount"));
    }

    [Fact]
    public void Validate_AnteEnabledWithZeroPercentage_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with
        {
            AnteBlindRules = new AnteBlindRulesDto(HasAnte: true, AntePercentage: 0)
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("AntePercentage") && e.Contains("positive"));
    }

    [Fact]
    public void Validate_WildcardsEnabledWithoutCards_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with
        {
            WildcardRules = new WildcardRulesDto(Enabled: true, Dynamic: false, WildcardCards: null)
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("WildcardCards"));
    }

    [Fact]
    public void Validate_DynamicWildcardsWithoutRule_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with
        {
            WildcardRules = new WildcardRulesDto(Enabled: true, Dynamic: true, DynamicRule: null)
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("DynamicRule"));
    }

    [Fact]
    public void Validate_HiLoInvalidQualifier_ReturnsError()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with
        {
            HiLoRules = new HiLoRulesDto(Enabled: true, LowQualifier: 10)
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("LowQualifier"));
    }

    [Fact]
    public void Validate_CommunityCardsMismatch_ReturnsError()
    {
        // Arrange - Total dealt in rounds doesn't match TotalCount
        var ruleSet = CreateValidBaseRuleSet() with
        {
            CommunityCardRules = new CommunityCardRulesDto(TotalCount: 5, MinUsedInHand: 0, MaxUsedInHand: 5),
            BettingRounds = new List<BettingRoundDto>
            {
                new("Preflop", 0, CommunityCardsDealt: 0),
                new("Flop", 1, CommunityCardsDealt: 3),
                // Missing turn and river means only 3 community cards dealt
            }
        };

        // Act
        var errors = RuleSetValidator.Validate(ruleSet);

        // Assert
        errors.Should().Contain(e => e.Contains("community cards dealt"));
    }

    [Fact]
    public void ValidateAndThrow_InvalidRuleSet_ThrowsValidationException()
    {
        // Arrange
        var ruleSet = CreateValidBaseRuleSet() with { Id = "" };

        // Act & Assert
        var act = () => RuleSetValidator.ValidateAndThrow(ruleSet);
        act.Should().Throw<RuleSetValidationException>()
            .Which.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateAndThrow_ValidRuleSet_DoesNotThrow()
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.TexasHoldem;

        // Act & Assert
        var act = () => RuleSetValidator.ValidateAndThrow(ruleSet);
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_NullRuleSet_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => RuleSetValidator.Validate(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static RuleSetDto CreateValidBaseRuleSet()
    {
        return new RuleSetDto(
            SchemaVersion: "1.0",
            Id: "test-ruleset",
            Name: "Test Ruleset",
            Variant: PokerVariant.TexasHoldem,
            DeckComposition: new DeckCompositionDto(DeckType.Full52, NumberOfDecks: 1),
            CardVisibility: new CardVisibilityDto(HoleCardsPrivate: true, CommunityCardsPublic: true),
            BettingRounds: new List<BettingRoundDto>
            {
                new("Preflop", 0, HoleCardsDealt: 2),
                new("Flop", 1, CommunityCardsDealt: 3),
                new("Turn", 2, CommunityCardsDealt: 1),
                new("River", 3, CommunityCardsDealt: 1)
            },
            HoleCardRules: new HoleCardRulesDto(Count: 2, MinUsedInHand: 0, MaxUsedInHand: 2),
            CommunityCardRules: new CommunityCardRulesDto(TotalCount: 5, MinUsedInHand: 0, MaxUsedInHand: 5)
        );
    }
}
