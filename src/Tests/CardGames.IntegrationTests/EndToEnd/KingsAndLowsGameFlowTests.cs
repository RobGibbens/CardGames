using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;
using Microsoft.EntityFrameworkCore;
using DropOrStayDecision = CardGames.Poker.Api.Data.Entities.DropOrStayDecision;

namespace CardGames.IntegrationTests.EndToEnd;

/// <summary>
/// End-to-end integration tests for Kings and Lows game flow.
/// Tests the unique phase sequence including DropOrStay and PotMatching.
/// </summary>
public class KingsAndLowsGameFlowTests : IntegrationTestBase
{
    [Fact]
    public async Task FullGameFlow_StartHand_TransitionsToDealing()
    {
        // Arrange - Kings and Lows starts with Dealing, not CollectingAntes
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);

        // Act
        var result = await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.CurrentPhase.Should().Be(nameof(Phases.Dealing));
    }

    [Fact]
    public async Task StartHand_ResetsDropOrStayDecisions()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        
        // Simulate previous hand with decisions
        foreach (var gp in setup.GamePlayers)
        {
            gp.DropOrStayDecision = DropOrStayDecision.Stay;
        }
        await DbContext.SaveChangesAsync();

        // Act
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Assert - All decisions should be reset
        var players = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == setup.Game.Id)
            .ToListAsync();

        players.Should().AllSatisfy(gp => gp.DropOrStayDecision.Should().Be(DropOrStayDecision.Undecided));
    }

    [Fact]
    public async Task FlowHandler_GetNextPhase_FromDealing_ReturnsDropOrStay()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        var handler = FlowHandlerFactory.GetHandler("KINGSANDLOWS");

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.Dealing));

        // Assert
        nextPhase.Should().Be(nameof(Phases.DropOrStay));
    }

    [Fact]
    public async Task FlowHandler_GetNextPhase_FromDropOrStay_DeterminesBasedOnPlayerCount()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        var handler = FlowHandlerFactory.GetHandler("KINGSANDLOWS");

        // Scenario 1: Multiple players stay
        foreach (var gp in setup.GamePlayers)
        {
            gp.DropOrStayDecision = DropOrStayDecision.Stay;
            gp.HasFolded = false;
        }
        await DbContext.SaveChangesAsync();
        
        await DbContext.Entry(setup.Game).ReloadAsync();

        var nextPhaseMultiple = handler.GetNextPhase(setup.Game, nameof(Phases.DropOrStay));
        nextPhaseMultiple.Should().Be(nameof(Phases.DrawPhase));

        // Scenario 2: Only one player stays
        foreach (var gp in setup.GamePlayers.Skip(1))
        {
            gp.DropOrStayDecision = DropOrStayDecision.Drop;
            gp.HasFolded = true;
        }
        await DbContext.SaveChangesAsync();
        
        // Reload collection to ensure HasFolded status is updated in the navigation property
        DbContext.ChangeTracker.Clear();
        var game = await DbContext.Games.Include(g => g.GamePlayers).FirstAsync(g => g.Id == setup.Game.Id);

        var nextPhaseSingle = handler.GetNextPhase(game, nameof(Phases.DropOrStay));
        nextPhaseSingle.Should().Be(nameof(Phases.PlayerVsDeck));

        // Scenario 3: All players drop
        // We must update the instance in the 'game' variable's collection, OR update DB and reload 'game' again.
        // Since ChangeTracker was cleared in previous step, setup.GamePlayers are detached. We must fetch the entity.
        var player0 = await DbContext.GamePlayers.FirstAsync(gp => gp.Id == setup.GamePlayers[0].Id);
        player0.DropOrStayDecision = DropOrStayDecision.Drop;
        player0.HasFolded = true;
        await DbContext.SaveChangesAsync();

        game = await DbContext.Games.Include(g => g.GamePlayers).FirstAsync(g => g.Id == setup.Game.Id);
        
        var nextPhaseNone = handler.GetNextPhase(game, nameof(Phases.DropOrStay));
        nextPhaseNone.Should().Be(nameof(Phases.Complete));
    }

    [Fact]
    public async Task FlowHandler_GetNextPhase_FromShowdown_ReturnsPotMatching()
    {
        // Arrange - Kings and Lows has pot matching after showdown
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        var handler = FlowHandlerFactory.GetHandler("KINGSANDLOWS");

        // Act
        var nextPhase = handler.GetNextPhase(setup.Game, nameof(Phases.Showdown));

        // Assert
        nextPhase.Should().Be(nameof(Phases.PotMatching));
    }

    [Fact]
    public async Task FlowHandler_SkipsAnteCollection_ReturnsTrue()
    {
        // Arrange - Kings and Lows doesn't collect antes traditionally
        var handler = FlowHandlerFactory.GetHandler("KINGSANDLOWS");

        // Assert
        handler.SkipsAnteCollection.Should().BeTrue();
    }

    [Fact]
    public async Task FlowHandler_RequiresChipCoverageCheck_ReturnsTrue()
    {
        // Arrange - Kings and Lows requires chip coverage check
        var handler = FlowHandlerFactory.GetHandler("KINGSANDLOWS");

        // Assert
        handler.RequiresChipCoverageCheck.Should().BeTrue();
    }

    [Fact]
    public async Task FlowHandler_GetChipCheckConfiguration_ReturnsValidConfig()
    {
        // Arrange
        var handler = FlowHandlerFactory.GetHandler("KINGSANDLOWS") as KingsAndLowsFlowHandler;
        handler.Should().NotBeNull();

        // Act
        var config = handler!.GetChipCheckConfiguration();

        // Assert
        config.IsEnabled.Should().BeTrue();
        config.PauseDuration.Should().BeGreaterThan(TimeSpan.Zero);
        config.ShortageAction.Should().Be(ChipShortageAction.AutoDrop);
    }

    [Fact]
    public async Task FlowHandler_SupportsInlineShowdown_ReturnsTrue()
    {
        // Arrange - Kings and Lows supports inline showdown in background service
        var handler = FlowHandlerFactory.GetHandler("KINGSANDLOWS");

        // Assert
        handler.SupportsInlineShowdown.Should().BeTrue();
    }

    [Fact]
    public async Task FlowHandler_SpecialPhases_ContainsAllKingsAndLowsPhases()
    {
        // Arrange
        var handler = FlowHandlerFactory.GetHandler("KINGSANDLOWS");

        // Assert
        handler.SpecialPhases.Should().Contain(nameof(Phases.DropOrStay));
        handler.SpecialPhases.Should().Contain(nameof(Phases.PotMatching));
        handler.SpecialPhases.Should().Contain(nameof(Phases.PlayerVsDeck));
    }

    [Fact]
    public async Task DealCards_AfterStartHand_TransitionsToDropOrStay()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        var game = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
        var handler = FlowHandlerFactory.GetHandler("KINGSANDLOWS");

        // Act - Deal cards
        await handler.DealCardsAsync(
            DbContext,
            game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert
        game.CurrentPhase.Should().Be(nameof(Phases.DropOrStay));
    }

    [Fact]
    public async Task CompletePhaseSequence_MultiplePlayersStaying()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        var handler = FlowHandlerFactory.GetHandler("KINGSANDLOWS");

        // Simulate all players staying
        foreach (var gp in setup.GamePlayers)
        {
            gp.DropOrStayDecision = DropOrStayDecision.Stay;
        }
        await DbContext.SaveChangesAsync();

        // Act & Assert - Verify complete phase sequence
        handler.GetNextPhase(setup.Game, nameof(Phases.Dealing))
            .Should().Be(nameof(Phases.DropOrStay));
        
        handler.GetNextPhase(setup.Game, nameof(Phases.DropOrStay))
            .Should().Be(nameof(Phases.DrawPhase));
        
        handler.GetNextPhase(setup.Game, nameof(Phases.DrawPhase))
            .Should().Be(nameof(Phases.DrawComplete));
        
        handler.GetNextPhase(setup.Game, nameof(Phases.DrawComplete))
            .Should().Be(nameof(Phases.Showdown));
        
        handler.GetNextPhase(setup.Game, nameof(Phases.Showdown))
            .Should().Be(nameof(Phases.PotMatching));
    }

    [Fact]
    public async Task CompletePhaseSequence_SinglePlayerStaying()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        var handler = FlowHandlerFactory.GetHandler("KINGSANDLOWS");

        // Simulate only first player staying
        setup.GamePlayers[0].DropOrStayDecision = DropOrStayDecision.Stay;
        setup.GamePlayers[0].HasFolded = false;
        for (var i = 1; i < setup.GamePlayers.Count; i++)
        {
            setup.GamePlayers[i].DropOrStayDecision = DropOrStayDecision.Drop;
            setup.GamePlayers[i].HasFolded = true;
        }
        await DbContext.SaveChangesAsync();

        // Refresh game to ensure proper state for phase determination
        DbContext.ChangeTracker.Clear();
        var game = await DbContext.Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == setup.Game.Id);
            
        // Act & Assert - Single player goes to PlayerVsDeck
        handler.GetNextPhase(game, nameof(Phases.DropOrStay))
            .Should().Be(nameof(Phases.PlayerVsDeck));
        
        handler.GetNextPhase(game, nameof(Phases.PlayerVsDeck))
            .Should().Be(nameof(Phases.Complete));
    }

    [Fact]
    public async Task MultipleHandsInSequence_MaintainsCorrectState()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);

        // Hand 1
        var result1 = await Mediator.Send(new StartHandCommand(setup.Game.Id));
        result1.IsT0.Should().BeTrue();
        result1.AsT0.HandNumber.Should().Be(1);
        result1.AsT0.CurrentPhase.Should().Be(nameof(Phases.Dealing));

        // Complete hand 1
        var game = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase = nameof(Phases.Complete);
        await DbContext.SaveChangesAsync();

        // Hand 2
        var result2 = await Mediator.Send(new StartHandCommand(setup.Game.Id));
        result2.IsT0.Should().BeTrue();
        result2.AsT0.HandNumber.Should().Be(2);
        result2.AsT0.CurrentPhase.Should().Be(nameof(Phases.Dealing));

        // Verify DropOrStay decisions are reset between hands
        var players = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == setup.Game.Id)
            .ToListAsync();
        
        players.Should().AllSatisfy(gp => gp.DropOrStayDecision.Should().Be(DropOrStayDecision.Undecided));
    }
}
