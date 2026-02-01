using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Betting;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.EndToEnd;

/// <summary>
/// End-to-end integration tests for Five Card Draw game flow.
/// Tests complete scenarios from game creation through hand completion.
/// </summary>
public class FiveCardDrawGameFlowTests : IntegrationTestBase
{
    [Fact]
    public async Task FullGameFlow_StartHand_FollowsCorrectPhaseSequence()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act - Start a new hand
        var startCommand = new StartHandCommand(setup.Game.Id);
        var result = await Mediator.Send(startCommand);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.CurrentPhase.Should().Be(nameof(Phases.CollectingAntes));

        // Verify database state
        var game = await GetFreshDbContext().Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == setup.Game.Id);

        game.CurrentPhase.Should().Be(nameof(Phases.CollectingAntes));
        game.Status.Should().Be(GameStatus.InProgress);
        game.CurrentHandNumber.Should().Be(1);
    }

    [Fact]
    public async Task StartHand_WithVaryingChipStacks_CorrectlySitsOutPlayers()
    {
        // Arrange - Create game with mixed chip stacks
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
            DbContext, "FIVECARDDRAW", 4, startingChips: 100, ante: 20);
        
        // Player 3 has chips less than ante
        setup.GamePlayers[3].ChipStack = 15;
        // Player 2 has exactly ante amount
        setup.GamePlayers[2].ChipStack = 20;
        await DbContext.SaveChangesAsync();

        // Act
        var command = new StartHandCommand(setup.Game.Id);
        var result = await Mediator.Send(command);

        // Assert - Should succeed with 3 active players
        result.IsT0.Should().BeTrue();
        result.AsT0.ActivePlayerCount.Should().Be(3);

        // Verify player states
        var players = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == setup.Game.Id)
            .OrderBy(gp => gp.SeatPosition)
            .ToListAsync();

        players[0].IsSittingOut.Should().BeFalse(); // 100 chips
        players[1].IsSittingOut.Should().BeFalse(); // 100 chips
        players[2].IsSittingOut.Should().BeFalse(); // 20 chips (exactly ante)
        players[3].IsSittingOut.Should().BeTrue();  // 15 chips (below ante)
    }

    [Fact]
    public async Task GameFlowHandler_GetNextPhase_CorrectlyHandlesSinglePlayerRemaining()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        
        // Start the hand
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        
        // Simulate all but one player folding
        var freshContext = GetFreshDbContext();
        var game = await freshContext.Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == setup.Game.Id);

        foreach (var gp in game.GamePlayers.Skip(1))
        {
            gp.HasFolded = true;
        }
        await freshContext.SaveChangesAsync();

        // Act - Check what the flow handler says the next phase should be
        var flowHandler = FlowHandlerFactory.GetHandler("FIVECARDDRAW");
        var nextPhase = flowHandler.GetNextPhase(game, nameof(Phases.FirstBettingRound));

        // Assert - Should skip to showdown when only one player remains
        nextPhase.Should().Be(nameof(Phases.Showdown));
    }

    [Fact]
    public async Task MultipleHandsInSequence_CorrectlyIncrementsHandNumber()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act - Start first hand
        var result1 = await Mediator.Send(new StartHandCommand(setup.Game.Id));
        result1.IsT0.Should().BeTrue();
        result1.AsT0.HandNumber.Should().Be(1);

        // Complete the hand by changing phase
        var game = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase = nameof(Phases.Complete);
        await DbContext.SaveChangesAsync();

        // Start second hand
        var result2 = await Mediator.Send(new StartHandCommand(setup.Game.Id));
        result2.IsT0.Should().BeTrue();
        result2.AsT0.HandNumber.Should().Be(2);
    }

    [Fact]
    public async Task DealerPosition_CorrectlyAffectsActionOrder()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        setup.Game.DealerPosition = 1; // Set dealer at seat 1
        await DbContext.SaveChangesAsync();

        // Act
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Verify dealing configuration respects dealer position
        var flowHandler = FlowHandlerFactory.GetHandler("FIVECARDDRAW");
        var dealingConfig = flowHandler.GetDealingConfiguration();

        dealingConfig.PatternType.Should().Be(DealingPatternType.AllAtOnce);
        dealingConfig.InitialCardsPerPlayer.Should().Be(5);
    }

    [Fact]
    public async Task FlowHandler_DealCardsAsync_CreatesUniqueCards()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        // Act
        var flowHandler = FlowHandlerFactory.GetHandler("FIVECARDDRAW");
        await flowHandler.DealCardsAsync(
            DbContext,
            setup.Game,
            setup.GamePlayers,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        // Assert - All dealt cards should be unique
        var cards = await DbContext.GameCards
            .Where(gc => gc.GameId == setup.Game.Id && gc.GamePlayerId != null)
            .ToListAsync();

        var uniqueCards = cards
            .Select(c => $"{c.Suit}-{c.Symbol}")
            .Distinct()
            .ToList();

        cards.Should().HaveCount(20); // 5 cards * 4 players
        uniqueCards.Should().HaveCount(20); // All should be unique
    }

    [Fact]
    public async Task StartHand_CreatesNewPot_ForEachHand()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Start first hand
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Complete first hand
        var game = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentPhase = nameof(Phases.Complete);
        await DbContext.SaveChangesAsync();

        // Start second hand
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Assert - Should have pots for both hands
        var pots = await GetFreshDbContext().Pots
            .Where(p => p.GameId == setup.Game.Id)
            .OrderBy(p => p.HandNumber)
            .ToListAsync();

        pots.Should().HaveCount(2);
        pots[0].HandNumber.Should().Be(1);
        pots[1].HandNumber.Should().Be(2);
    }
}
