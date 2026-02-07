using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using FluentAssertions;
using Xunit;

namespace CardGames.IntegrationTests.GameFlow;

public class GameFlowHandlerTests
{
    [Fact]
    public void Factory_CanResolve_HoldEmHandler()
    {
        // Arrange
        var factory = new GameFlowHandlerFactory();

        // Act
        var handler = factory.GetHandler(PokerGameMetadataRegistry.HoldEmCode);

        // Assert
        handler.Should().NotBeNull();
        handler.Should().BeOfType<HoldEmFlowHandler>();
        handler.GameTypeCode.Should().Be("HOLDEM");
        handler.SkipsAnteCollection.Should().BeTrue();
    }

    [Fact]
    public void Factory_CanResolve_OmahaHandler()
    {
        // Arrange
        var factory = new GameFlowHandlerFactory();

        // Act
        var handler = factory.GetHandler(PokerGameMetadataRegistry.OmahaCode);

        // Assert
        handler.Should().NotBeNull();
        handler.Should().BeOfType<OmahaFlowHandler>();
        handler.GameTypeCode.Should().Be("OMAHA");
    }

    [Fact]
    public void Factory_CanResolve_FollowTheQueenHandler()
    {
        // Arrange
        var factory = new GameFlowHandlerFactory();

        // Act
        var handler = factory.GetHandler(PokerGameMetadataRegistry.FollowTheQueenCode);

        // Assert
        handler.Should().NotBeNull();
        handler.Should().BeOfType<FollowTheQueenFlowHandler>();
        handler.GameTypeCode.Should().Be("FOLLOWTHEQUEEN");
    }
}
