using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.GameFlow;

namespace CardGames.IntegrationTests.GameFlow;

/// <summary>
/// Integration tests for <see cref="GameFlowHandlerFactory"/>.
/// Tests handler resolution and registration via assembly scanning.
/// </summary>
public class GameFlowHandlerFactoryTests : IntegrationTestBase
{
    [Theory]
    [InlineData("FIVECARDDRAW", typeof(FiveCardDrawFlowHandler))]
    [InlineData("SEVENCARDSTUD", typeof(SevenCardStudFlowHandler))]
    [InlineData("KINGSANDLOWS", typeof(KingsAndLowsFlowHandler))]
    [InlineData("TWOSJACKSMANWITHTHEAXE", typeof(TwosJacksManWithTheAxeFlowHandler))]
    public void GetHandler_ReturnsCorrectHandlerType(string gameTypeCode, Type expectedType)
    {
        // Act
        var handler = FlowHandlerFactory.GetHandler(gameTypeCode);

        // Assert
        handler.Should().BeOfType(expectedType);
        handler.GameTypeCode.Should().Be(gameTypeCode);
    }

    [Theory]
    [InlineData("fivecarddraw")]
    [InlineData("FiveCardDraw")]
    [InlineData("FIVECARDDRAW")]
    public void GetHandler_IsCaseInsensitive(string gameTypeCode)
    {
        // Act
        var handler = FlowHandlerFactory.GetHandler(gameTypeCode);

        // Assert
        handler.Should().BeOfType<FiveCardDrawFlowHandler>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UNKNOWNGAME")]
    [InlineData("INVALID")]
    public void GetHandler_WithUnknownGameType_ReturnsFallbackHandler(string gameTypeCode)
    {
        // Act
        var handler = FlowHandlerFactory.GetHandler(gameTypeCode);

        // Assert - Default fallback is FiveCardDrawFlowHandler
        handler.Should().BeOfType<FiveCardDrawFlowHandler>();
    }

    [Fact]
    public void GetHandler_WithNullGameType_ReturnsFallbackHandler()
    {
        // Act
        var handler = FlowHandlerFactory.GetHandler(null!);

        // Assert
        handler.Should().BeOfType<FiveCardDrawFlowHandler>();
    }

    [Theory]
    [InlineData("FIVECARDDRAW", true)]
    [InlineData("SEVENCARDSTUD", true)]
    [InlineData("KINGSANDLOWS", true)]
    [InlineData("TWOSJACKSMANWITHTHEAXE", true)]
    [InlineData("UNKNOWNGAME", false)]
    [InlineData("", false)]
    public void TryGetHandler_ReturnsExpectedResult(string gameTypeCode, bool expectedResult)
    {
        // Act
        var result = FlowHandlerFactory.TryGetHandler(gameTypeCode, out var handler);

        // Assert
        result.Should().Be(expectedResult);
        if (expectedResult)
        {
            handler.Should().NotBeNull();
            handler!.GameTypeCode.Should().BeEquivalentTo(gameTypeCode);
        }
        else
        {
            handler.Should().BeNull();
        }
    }

    [Fact]
    public void TryGetHandler_WithNull_ReturnsFalse()
    {
        // Act
        var result = FlowHandlerFactory.TryGetHandler(null!, out var handler);

        // Assert
        result.Should().BeFalse();
        handler.Should().BeNull();
    }

    [Fact]
    public void AllHandlers_HaveValidGameRules()
    {
        // Arrange
        var gameTypeCodes = new[] { "FIVECARDDRAW", "SEVENCARDSTUD", "KINGSANDLOWS", "TWOSJACKSMANWITHTHEAXE" };

        foreach (var code in gameTypeCodes)
        {
            // Act
            var handler = FlowHandlerFactory.GetHandler(code);
            var rules = handler.GetGameRules();

            // Assert
            rules.Should().NotBeNull($"{code} should have valid rules");
            rules.Phases.Should().NotBeEmpty($"{code} should have defined phases");
        }
    }

    [Fact]
    public void AllHandlers_HaveValidDealingConfiguration()
    {
        // Arrange
        var gameTypeCodes = new[] { "FIVECARDDRAW", "SEVENCARDSTUD", "KINGSANDLOWS", "TWOSJACKSMANWITHTHEAXE" };

        foreach (var code in gameTypeCodes)
        {
            // Act
            var handler = FlowHandlerFactory.GetHandler(code);
            var config = handler.GetDealingConfiguration();

            // Assert
            config.Should().NotBeNull($"{code} should have valid dealing configuration");
            
            if (config.PatternType == DealingPatternType.StreetBased)
            {
                config.DealingRounds.Should().NotBeNullOrEmpty($"{code} street-based game should have dealing rounds");
            }
            else
            {
                config.InitialCardsPerPlayer.Should().BeGreaterThan(0, 
                    $"{code} should deal at least 1 card per player");
            }
        }
    }

    [Fact]
    public void GetHandler_ReturnsConsistentInstances()
    {
        // Factory may return new instances or cached - verify behavior is consistent
        var handler1 = FlowHandlerFactory.GetHandler("FIVECARDDRAW");
        var handler2 = FlowHandlerFactory.GetHandler("FIVECARDDRAW");

        // Both should have same game type code regardless of instance equality
        handler1.GameTypeCode.Should().Be(handler2.GameTypeCode);
        handler1.GetDealingConfiguration().PatternType.Should().Be(
            handler2.GetDealingConfiguration().PatternType);
    }
}
