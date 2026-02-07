using CardGames.Poker.Api.Games;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Api;

public class PokerGameRulesRegistryTests
{
    [Fact]
    public void Registry_ShouldContainSevenCardStud()
    {
        // Act
        var success = PokerGameRulesRegistry.TryGet("SEVENCARDSTUD", out var rules);

        // Assert
        success.Should().BeTrue();
        rules.Should().NotBeNull();
        rules!.GameTypeCode.Should().Be("SEVENCARDSTUD");
        rules.GameTypeName.Should().Be("Seven Card Stud");
    }

    [Fact]
    public void Registry_ShouldReturnSevenCardStudRules()
    {
        // Act
        var rules = PokerGameRulesRegistry.Get("SEVENCARDSTUD");

        // Assert
        rules.Should().NotBeNull();
        rules.GameTypeCode.Should().Be("SEVENCARDSTUD");
        rules.MinPlayers.Should().Be(2);
        rules.MaxPlayers.Should().Be(7);
        rules.Phases.Should().HaveCount(9);
    }

    [Fact]
    public void Registry_IsAvailable_ShouldReturnTrueForSevenCardStud()
    {
        // Act
        var isAvailable = PokerGameRulesRegistry.IsAvailable("SEVENCARDSTUD");

        // Assert
        isAvailable.Should().BeTrue();
    }

    [Fact]
    public void Registry_GetAvailableGameTypeCodes_ShouldIncludeSevenCardStud()
    {
        // Act
        var codes = PokerGameRulesRegistry.GetAvailableGameTypeCodes();

        // Assert
        codes.Should().Contain("SEVENCARDSTUD");
    }

    [Theory]
    [InlineData("SEVENCARDSTUD")]
    [InlineData("sevencardstud")]
    [InlineData("SevenCardStud")]
    public void Registry_ShouldBeCaseInsensitive(string gameTypeCode)
    {
        // Act
        var success = PokerGameRulesRegistry.TryGet(gameTypeCode, out var rules);

        // Assert
        success.Should().BeTrue();
        rules.Should().NotBeNull();
        rules!.GameTypeCode.Should().Be("SEVENCARDSTUD");
    }

    [Theory]
    [InlineData("SEVENCARDSTUD")]
    [InlineData("FIVECARDDRAW")]
    [InlineData("TWOSJACKSMANWITHTHEAXE")]
    [InlineData("KINGSANDLOWS")]
    [InlineData("OMAHA")]
    [InlineData("HOLDEM")]
    [InlineData("BASEBALL")]
    [InlineData("FOLLOWTHEQUEEN")]
    public void Registry_ShouldContainGameType(string gameTypeCode)
    {
        // Act
        var success = PokerGameRulesRegistry.TryGet(gameTypeCode, out var rules);

        // Assert
        success.Should().BeTrue();
        rules.Should().NotBeNull();
        rules!.GameTypeCode.Should().Be(gameTypeCode);
    }

    [Fact]
    public void Registry_GetAvailableGameTypeCodes_ShouldIncludeAllGames()
    {
        // Act
        var codes = PokerGameRulesRegistry.GetAvailableGameTypeCodes();

        // Assert
        codes.Should().Contain(new[] {
            "SEVENCARDSTUD",
            "FIVECARDDRAW",
            "TWOSJACKSMANWITHTHEAXE",
            "KINGSANDLOWS",
            "OMAHA",
            "HOLDEM",
            "BASEBALL",
            "FOLLOWTHEQUEEN"
        });
    }
}
