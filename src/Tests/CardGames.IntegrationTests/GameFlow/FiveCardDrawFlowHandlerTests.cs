using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;

namespace CardGames.IntegrationTests.GameFlow;

/// <summary>
/// Integration tests for <see cref="FiveCardDrawFlowHandler"/>.
/// Tests phase transitions, dealing configuration, and game-specific behavior.
/// </summary>
public class FiveCardDrawFlowHandlerTests : IntegrationTestBase
{
    [Fact]
    public void GameTypeCode_ReturnsFiveCardDraw()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();

        // Assert
        handler.GameTypeCode.Should().Be("FIVECARDDRAW");
    }

    [Fact]
    public void GetGameRules_ReturnsValidRules()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();

        // Act
        var rules = handler.GetGameRules();

        // Assert
        rules.Should().NotBeNull();
        rules.Phases.Should().NotBeEmpty();
    }

    [Fact]
    public void GetDealingConfiguration_ReturnsAllAtOnceWith5Cards()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();

        // Act
        var config = handler.GetDealingConfiguration();

        // Assert
        config.PatternType.Should().Be(DealingPatternType.AllAtOnce);
        config.InitialCardsPerPlayer.Should().Be(5);
        config.AllFaceDown.Should().BeTrue();
    }

    [Fact]
    public async Task GetInitialPhase_ReturnsCollectingAntes()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act
        var initialPhase = handler.GetInitialPhase(setup.Game);

        // Assert
        initialPhase.Should().Be(nameof(Phases.CollectingAntes));
    }

    [Fact]
    public async Task GetNextPhase_FromCollectingAntes_ReturnsDealing()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.CollectingAntes));

        // Assert
        nextPhase.Should().Be(nameof(Phases.Dealing));
    }

    [Fact]
    public async Task GetNextPhase_FromDealing_ReturnsFirstBettingRound()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.Dealing));

        // Assert
        nextPhase.Should().Be(nameof(Phases.FirstBettingRound));
    }

    [Fact]
    public async Task GetNextPhase_FromFirstBettingRound_ReturnsDrawPhase()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.FirstBettingRound));

        // Assert
        nextPhase.Should().Be(nameof(Phases.DrawPhase));
    }

    [Fact]
    public async Task GetNextPhase_FromDrawPhase_ReturnsDrawComplete()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.DrawPhase));

        // Assert
        nextPhase.Should().Be(nameof(Phases.DrawComplete));
    }

    [Fact]
    public async Task GetNextPhase_FromSecondBettingRound_ReturnsShowdown()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.SecondBettingRound));

        // Assert
        nextPhase.Should().Be(nameof(Phases.Showdown));
    }

    [Fact]
    public async Task GetNextPhase_FromShowdown_ReturnsComplete()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.Showdown));

        // Assert
        nextPhase.Should().Be(nameof(Phases.Complete));
    }

    [Fact]
    public async Task GetNextPhase_SinglePlayerRemaining_SkipsToShowdown()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Simulate all but one player folding
        foreach (var gp in setup.GamePlayers.Skip(1))
        {
            gp.HasFolded = true;
        }
        await DbContext.SaveChangesAsync();

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.FirstBettingRound));

        // Assert
        nextPhase.Should().Be(nameof(Phases.Showdown));
    }

    [Fact]
    public void SkipsAnteCollection_ReturnsFalse()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();

        // Assert
        handler.SkipsAnteCollection.Should().BeFalse();
    }

    [Fact]
    public void SpecialPhases_IsEmpty()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();

        // Assert
        handler.SpecialPhases.Should().BeEmpty();
    }

    [Fact]
    public void SupportsInlineShowdown_ReturnsFalse()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();

        // Assert
        handler.SupportsInlineShowdown.Should().BeFalse();
    }

    [Fact]
    public void RequiresChipCoverageCheck_ReturnsFalse()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();

        // Assert
        handler.RequiresChipCoverageCheck.Should().BeFalse();
    }

    [Fact]
    public async Task OnHandStartingAsync_DoesNotThrow()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act & Assert - should complete without throwing
        await handler.OnHandStartingAsync(setup.Game);
    }

    [Fact]
    public async Task OnHandCompletedAsync_DoesNotThrow()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act & Assert - should complete without throwing
        await handler.OnHandCompletedAsync(setup.Game);
    }

    [Fact]
    public async Task DealCardsAsync_Creates5CardsPerPlayer()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        // Act
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert
        var cards = DbContext.GameCards.Where(gc => gc.GameId == setup.Game.Id).ToList();
        var playerCards = cards.Where(c => c.GamePlayerId != null && c.Location == CardLocation.Hand).ToList();

        playerCards.Should().HaveCount(20); // 5 cards * 4 players
        foreach (var player in setup.GamePlayers)
        {
            var cardsForPlayer = cards.Count(c => c.GamePlayerId == player.Id && c.Location == CardLocation.Hand);
            cardsForPlayer.Should().Be(5);
        }
    }

    [Fact]
    public async Task DealCardsAsync_AllCardsAreFaceDown()
    {
        // Arrange
        var handler = new FiveCardDrawFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        // Act
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert
        var playerCards = DbContext.GameCards
            .Where(gc => gc.GameId == setup.Game.Id && gc.GamePlayerId != null)
            .ToList();

        playerCards.Should().AllSatisfy(c => c.IsVisible.Should().BeFalse());
    }
}
