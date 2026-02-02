using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.EndToEnd;

/// <summary>
/// End-to-end integration tests for Seven Card Stud game flow.
/// Tests street-based dealing and phase sequence.
/// </summary>
public class SevenCardStudGameFlowTests : IntegrationTestBase
{
    [Fact]
    public async Task FullGameFlow_StartHand_TransitionsToCollectingAntes()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);

        // Act
        var result = await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.CurrentPhase.Should().Be(nameof(Phases.CollectingAntes));
    }

    [Fact]
    public async Task FlowHandler_GetNextPhase_FollowsStreetSequence()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);
        var handler = FlowHandlerFactory.GetHandler("SEVENCARDSTUD");

        // Act & Assert - Verify street sequence
        handler.GetNextPhase(setup.Game, nameof(Phases.CollectingAntes))
            .Should().Be(nameof(Phases.ThirdStreet));
        
        handler.GetNextPhase(setup.Game, nameof(Phases.ThirdStreet))
            .Should().Be(nameof(Phases.FourthStreet));
        
        handler.GetNextPhase(setup.Game, nameof(Phases.FourthStreet))
            .Should().Be(nameof(Phases.FifthStreet));
        
        handler.GetNextPhase(setup.Game, nameof(Phases.FifthStreet))
            .Should().Be(nameof(Phases.SixthStreet));
        
        handler.GetNextPhase(setup.Game, nameof(Phases.SixthStreet))
            .Should().Be(nameof(Phases.SeventhStreet));
        
        handler.GetNextPhase(setup.Game, nameof(Phases.SeventhStreet))
            .Should().Be(nameof(Phases.Showdown));
        
        handler.GetNextPhase(setup.Game, nameof(Phases.Showdown))
            .Should().Be(nameof(Phases.Complete));
    }

    [Fact]
    public async Task FlowHandler_DealingConfiguration_IsStreetBased()
    {
        // Arrange
        var handler = FlowHandlerFactory.GetHandler("SEVENCARDSTUD");

        // Act
        var config = handler.GetDealingConfiguration();

        // Assert
        config.PatternType.Should().Be(DealingPatternType.StreetBased);
        config.DealingRounds.Should().NotBeNull();
        config.DealingRounds.Should().HaveCount(5);
    }

    [Fact]
    public async Task FlowHandler_DealingRounds_HaveCorrectConfiguration()
    {
        // Arrange
        var handler = FlowHandlerFactory.GetHandler("SEVENCARDSTUD");
        var config = handler.GetDealingConfiguration();

        // Assert - Third Street: 2 hole + 1 board
        var thirdStreet = config.DealingRounds!.First(r => r.PhaseName == nameof(Phases.ThirdStreet));
        thirdStreet.HoleCards.Should().Be(2);
        thirdStreet.BoardCards.Should().Be(1);
        thirdStreet.HasBettingAfter.Should().BeTrue();

        // Fourth through Sixth Street: 0 hole + 1 board
        var laterStreets = config.DealingRounds
            .Where(r => r.PhaseName is nameof(Phases.FourthStreet) or 
                        nameof(Phases.FifthStreet) or nameof(Phases.SixthStreet))
            .ToList();
        
        laterStreets.Should().AllSatisfy(r =>
        {
            r.HoleCards.Should().Be(0);
            r.BoardCards.Should().Be(1);
        });

        // Seventh Street: 1 hole + 0 board (river is face down)
        var seventhStreet = config.DealingRounds.First(r => r.PhaseName == nameof(Phases.SeventhStreet));
        seventhStreet.HoleCards.Should().Be(1);
        seventhStreet.BoardCards.Should().Be(0);
    }

    [Fact]
    public async Task FlowHandler_IsStreetPhase_CorrectlyIdentifiesStreets()
    {
        // Assert - Street phases
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.ThirdStreet)).Should().BeTrue();
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.FourthStreet)).Should().BeTrue();
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.FifthStreet)).Should().BeTrue();
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.SixthStreet)).Should().BeTrue();
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.SeventhStreet)).Should().BeTrue();

        // Non-street phases
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.CollectingAntes)).Should().BeFalse();
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.Showdown)).Should().BeFalse();
        SevenCardStudFlowHandler.IsStreetPhase(nameof(Phases.Complete)).Should().BeFalse();
    }

    [Fact]
    public async Task DealCards_CreatesCorrectThirdStreetLayout()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        var handler = FlowHandlerFactory.GetHandler("SEVENCARDSTUD");

        // Act
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert - Third Street: 2 hole + 1 board per player
        var cards = await DbContext.GameCards
            .Where(gc => gc.GameId == setup.Game.Id && gc.GamePlayerId != null)
            .ToListAsync();

        // Total cards dealt should be 12 (3 cards * 4 players)
        cards.Where(c => c.Location != CardLocation.Deck).Should().HaveCount(12);

        // Hole cards should be 8 (2 * 4 players)
        cards.Count(c => c.Location == CardLocation.Hole).Should().Be(8);

        // Board cards should be 4 (1 * 4 players)
        cards.Count(c => c.Location == CardLocation.Board).Should().Be(4);
    }

    [Fact]
    public async Task DealCards_HoleCardsAreNotVisible()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        var handler = FlowHandlerFactory.GetHandler("SEVENCARDSTUD");

        // Act
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert
        var holeCards = await DbContext.GameCards
            .Where(gc => gc.GameId == setup.Game.Id && gc.Location == CardLocation.Hole)
            .ToListAsync();

        holeCards.Should().AllSatisfy(c => c.IsVisible.Should().BeFalse());
    }

    [Fact]
    public async Task DealCards_BoardCardsAreVisible()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        var handler = FlowHandlerFactory.GetHandler("SEVENCARDSTUD");

        // Act
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert
        var boardCards = await DbContext.GameCards
            .Where(gc => gc.GameId == setup.Game.Id && gc.Location == CardLocation.Board)
            .ToListAsync();

        boardCards.Should().AllSatisfy(c => c.IsVisible.Should().BeTrue());
    }

    [Fact]
    public async Task DealCards_SetsPhaseToThirdStreet()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        var handler = FlowHandlerFactory.GetHandler("SEVENCARDSTUD");

        // Act
        await handler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert
        setup.Game.CurrentPhase.Should().Be(nameof(Phases.ThirdStreet));
    }

    [Fact]
    public async Task GetNextPhase_SinglePlayerRemaining_SkipsToShowdown()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);
        var handler = FlowHandlerFactory.GetHandler("SEVENCARDSTUD");

        // Simulate all but one player folding
        foreach (var gp in setup.GamePlayers.Skip(1))
        {
            gp.HasFolded = true;
        }
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Reload game to ensure player state is reflected in navigation property
        var game = await DbContext.Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == setup.Game.Id);

        // Act - From any street, should skip to showdown
        var nextPhase = handler.GetNextPhase(game, nameof(Phases.FifthStreet));

        // Assert
        nextPhase.Should().Be(nameof(Phases.Showdown));
    }

    [Fact]
    public async Task FlowHandler_SupportsInlineShowdown_ReturnsFalse()
    {
        // Arrange - Seven Card Stud uses command handler for showdown
        var handler = FlowHandlerFactory.GetHandler("SEVENCARDSTUD");

        // Assert
        handler.SupportsInlineShowdown.Should().BeFalse();
    }

    [Fact]
    public async Task FlowHandler_SpecialPhases_IsEmpty()
    {
        // Arrange - Seven Card Stud has no special phases like DropOrStay
        var handler = FlowHandlerFactory.GetHandler("SEVENCARDSTUD");

        // Assert
        handler.SpecialPhases.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleHandsInSequence_MaintainsCorrectState()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);

        // Hand 1
        var result1 = await Mediator.Send(new StartHandCommand(setup.Game.Id));
        result1.IsT0.Should().BeTrue();
        result1.AsT0.HandNumber.Should().Be(1);
        result1.AsT0.CurrentPhase.Should().Be(nameof(Phases.CollectingAntes));

        // Complete hand 1
        var game = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase = nameof(Phases.Complete);
        await DbContext.SaveChangesAsync();

        // Hand 2
        var result2 = await Mediator.Send(new StartHandCommand(setup.Game.Id));
        result2.IsT0.Should().BeTrue();
        result2.AsT0.HandNumber.Should().Be(2);
        result2.AsT0.CurrentPhase.Should().Be(nameof(Phases.CollectingAntes));
    }
}
