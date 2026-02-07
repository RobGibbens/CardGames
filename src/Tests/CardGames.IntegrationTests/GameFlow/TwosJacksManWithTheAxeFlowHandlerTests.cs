using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;

namespace CardGames.IntegrationTests.GameFlow;

/// <summary>
/// Integration tests for <see cref="TwosJacksManWithTheAxeFlowHandler"/>.
/// Tests standard five-card draw flow with wild cards (2s, Jacks, King of Diamonds).
/// </summary>
public class TwosJacksManWithTheAxeFlowHandlerTests : IntegrationTestBase
{
    [Fact]
    public void GameTypeCode_ReturnsTwosJacksManWithTheAxe()
    {
        // Arrange
        var handler = new TwosJacksManWithTheAxeFlowHandler();

        // Assert
        handler.GameTypeCode.Should().Be("TWOSJACKSMANWITHTHEAXE");
    }

    [Fact]
    public void GetGameRules_ReturnsValidRules()
    {
        // Arrange
        var handler = new TwosJacksManWithTheAxeFlowHandler();

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
        var handler = new TwosJacksManWithTheAxeFlowHandler();

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
        var handler = new TwosJacksManWithTheAxeFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "TWOSJACKSMANWITHTHEAXE", 4);

        // Act
        var initialPhase = handler.GetInitialPhase(setup.Game);

        // Assert
        initialPhase.Should().Be(nameof(Phases.CollectingAntes));
    }

    [Fact]
    public async Task GetNextPhase_FollowsStandardFiveCardDrawSequence()
    {
        // Arrange
        var handler = new TwosJacksManWithTheAxeFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "TWOSJACKSMANWITHTHEAXE", 4);

        // Act & Assert - Verify standard draw poker sequence
        handler.GetNextPhase(setup.Game, nameof(Phases.CollectingAntes)).Should().Be(nameof(Phases.Dealing));
        handler.GetNextPhase(setup.Game, nameof(Phases.Dealing)).Should().Be(nameof(Phases.FirstBettingRound));
        handler.GetNextPhase(setup.Game, nameof(Phases.FirstBettingRound)).Should().Be(nameof(Phases.DrawPhase));
        handler.GetNextPhase(setup.Game, nameof(Phases.DrawPhase)).Should().Be(nameof(Phases.DrawComplete));
        handler.GetNextPhase(setup.Game, nameof(Phases.SecondBettingRound)).Should().Be(nameof(Phases.Showdown));
        handler.GetNextPhase(setup.Game, nameof(Phases.Showdown)).Should().Be(nameof(Phases.Complete));
    }

    [Fact]
    public async Task GetNextPhase_SinglePlayerRemaining_SkipsToShowdown()
    {
        // Arrange
        var handler = new TwosJacksManWithTheAxeFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "TWOSJACKSMANWITHTHEAXE", 4);

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
        var handler = new TwosJacksManWithTheAxeFlowHandler();

        // Assert
        handler.SkipsAnteCollection.Should().BeFalse();
    }

    [Fact]
    public void SpecialPhases_IsEmpty()
    {
        // Arrange
        var handler = new TwosJacksManWithTheAxeFlowHandler();

        // Assert - No special phases like DropOrStay or PotMatching
        handler.SpecialPhases.Should().BeEmpty();
    }

    [Fact]
    public void SupportsInlineShowdown_ReturnsFalse()
    {
        // Arrange
        var handler = new TwosJacksManWithTheAxeFlowHandler();

        // Assert
        handler.SupportsInlineShowdown.Should().BeFalse();
    }

    [Fact]
    public void RequiresChipCoverageCheck_ReturnsFalse()
    {
        // Arrange
        var handler = new TwosJacksManWithTheAxeFlowHandler();

        // Assert
        handler.RequiresChipCoverageCheck.Should().BeFalse();
    }

    [Fact]
    public async Task OnHandStartingAsync_DoesNotThrow()
    {
        // Arrange
        var handler = new TwosJacksManWithTheAxeFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "TWOSJACKSMANWITHTHEAXE", 4);

        // Act & Assert - should complete without throwing
        await handler.OnHandStartingAsync(setup.Game);
    }

    [Fact]
    public async Task OnHandCompletedAsync_DoesNotThrow()
    {
        // Arrange
        var handler = new TwosJacksManWithTheAxeFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "TWOSJACKSMANWITHTHEAXE", 4);

        // Act & Assert - should complete without throwing
        await handler.OnHandCompletedAsync(setup.Game);
    }

    [Fact]
    public async Task DealCardsAsync_Creates5CardsPerPlayer()
    {
        // Arrange
        var handler = new TwosJacksManWithTheAxeFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "TWOSJACKSMANWITHTHEAXE", 4);
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
    }

    [Fact]
    public async Task DealCardsAsync_AllCardsAreFaceDown()
    {
        // Arrange
        var handler = new TwosJacksManWithTheAxeFlowHandler();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "TWOSJACKSMANWITHTHEAXE", 4);
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
