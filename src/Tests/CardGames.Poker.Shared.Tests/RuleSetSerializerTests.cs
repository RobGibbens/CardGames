using CardGames.Poker.Shared.DTOs.RuleSets;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.RuleSets;
using CardGames.Poker.Shared.Serialization;
using CardGames.Poker.Shared.Validation;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Shared.Tests;

public class RuleSetSerializerTests
{
    [Fact]
    public void Serialize_ValidHoldemRuleSet_ProducesValidJson()
    {
        // Arrange
        var ruleSet = PredefinedRuleSets.TexasHoldem;

        // Act
        var json = RuleSetSerializer.Serialize(ruleSet);

        // Assert
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("texas-holdem-nolimit");
        json.Should().Contain("schemaVersion");
        json.Should().Contain("bettingRounds");
    }

    [Fact]
    public void Deserialize_ValidJson_ReturnsRuleSetDto()
    {
        // Arrange
        var original = PredefinedRuleSets.TexasHoldem;
        var json = RuleSetSerializer.Serialize(original);

        // Act
        var deserialized = RuleSetSerializer.Deserialize(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Id.Should().Be(original.Id);
        deserialized.Name.Should().Be(original.Name);
        deserialized.Variant.Should().Be(original.Variant);
        deserialized.SchemaVersion.Should().Be(original.SchemaVersion);
    }

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsCorrectly()
    {
        // Arrange
        var original = PredefinedRuleSets.Omaha;

        // Act
        var json = RuleSetSerializer.Serialize(original);
        var deserialized = RuleSetSerializer.Deserialize(json);

        // Assert
        deserialized.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void DeserializeAndValidate_ValidRuleSet_ReturnsValidatedRuleSet()
    {
        // Arrange
        var original = PredefinedRuleSets.TexasHoldem;
        var json = RuleSetSerializer.Serialize(original);

        // Act
        var result = RuleSetSerializer.DeserializeAndValidate(json);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void DeserializeAndValidate_InvalidRuleSet_ThrowsValidationException()
    {
        // Arrange - invalid ruleset JSON with missing required fields
        var invalidJson = """
            {
                "schemaVersion": "1.0",
                "id": "invalid",
                "name": "",
                "variant": "texasHoldem",
                "deckComposition": {
                    "deckType": "full52",
                    "numberOfDecks": 0
                },
                "cardVisibility": {
                    "holeCardsPrivate": true
                },
                "bettingRounds": [],
                "holeCardRules": {
                    "count": 0
                }
            }
            """;

        // Act & Assert
        var act = () => RuleSetSerializer.DeserializeAndValidate(invalidJson);
        act.Should().Throw<RuleSetValidationException>();
    }

    [Fact]
    public void TryDeserialize_ValidJson_ReturnsTrue()
    {
        // Arrange
        var json = RuleSetSerializer.Serialize(PredefinedRuleSets.TexasHoldem);

        // Act
        var success = RuleSetSerializer.TryDeserialize(json, out var ruleSet);

        // Assert
        success.Should().BeTrue();
        ruleSet.Should().NotBeNull();
    }

    [Fact]
    public void TryDeserialize_InvalidJson_ReturnsFalse()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act
        var success = RuleSetSerializer.TryDeserialize(invalidJson, out var ruleSet);

        // Assert
        success.Should().BeFalse();
        ruleSet.Should().BeNull();
    }

    [Fact]
    public void TryDeserialize_EmptyString_ReturnsFalse()
    {
        // Act
        var success = RuleSetSerializer.TryDeserialize("", out var ruleSet);

        // Assert
        success.Should().BeFalse();
        ruleSet.Should().BeNull();
    }

    [Fact]
    public void TryDeserializeAndValidate_ValidRuleSet_ReturnsTrue()
    {
        // Arrange
        var json = RuleSetSerializer.Serialize(PredefinedRuleSets.TexasHoldem);

        // Act
        var success = RuleSetSerializer.TryDeserializeAndValidate(json, out var ruleSet, out var errors);

        // Assert
        success.Should().BeTrue();
        ruleSet.Should().NotBeNull();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void TryDeserializeAndValidate_InvalidRuleSet_ReturnsFalseWithErrors()
    {
        // Arrange - ruleset with validation errors
        var invalidJson = """
            {
                "schemaVersion": "1.0",
                "id": "invalid",
                "name": "",
                "variant": "texasHoldem",
                "deckComposition": {
                    "deckType": "full52",
                    "numberOfDecks": 1
                },
                "cardVisibility": {
                    "holeCardsPrivate": true
                },
                "bettingRounds": [],
                "holeCardRules": {
                    "count": 2
                }
            }
            """;

        // Act
        var success = RuleSetSerializer.TryDeserializeAndValidate(invalidJson, out var ruleSet, out var errors);

        // Assert
        success.Should().BeFalse();
        ruleSet.Should().BeNull();
        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Deserialize_EmptyString_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => RuleSetSerializer.Deserialize("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Serialize_NullRuleSet_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => RuleSetSerializer.Serialize(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetDefaultOptions_ReturnsValidOptions()
    {
        // Act
        var options = RuleSetSerializer.GetDefaultOptions();

        // Assert
        options.Should().NotBeNull();
        options.WriteIndented.Should().BeTrue();
    }
}
