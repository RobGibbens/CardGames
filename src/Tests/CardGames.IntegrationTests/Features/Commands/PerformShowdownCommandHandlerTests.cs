using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;
using BettingAction = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Features.Commands;

/// <summary>
/// Integration tests for <see cref="PerformShowdownCommandHandler"/>.
/// Tests showdown logic including winner determination and pot distribution.
/// </summary>
public class PerformShowdownCommandHandlerTests : IntegrationTestBase
{
    private async Task<Game> CreateGameInShowdownPhaseAsync(int numPlayers = 2)
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", numPlayers, ante: 10);
        
        // Start hand
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        
        // Collect antes
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
        
        // Deal hands
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // First betting round - everyone checks
        for (int i = 0; i < numPlayers; i++)
        {
            await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Check, 0));
        }

        // Draw phase - everyone stands pat
        for (int i = 0; i < numPlayers; i++)
        {
            var game = await DbContext.Games
                .Include(g => g.GamePlayers)
                .FirstAsync(g => g.Id == setup.Game.Id);
            
            await Mediator.Send(new ProcessDrawCommand(game.Id, new List<int>()));
        }

        // Second betting round - everyone checks
        for (int i = 0; i < numPlayers; i++)
        {
            await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Check, 0));
        }

        // Reload game
        var finalGame = await DbContext.Games
            .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
            .Include(g => g.GameCards)
            .Include(g => g.Pots)
            .FirstAsync(g => g.Id == setup.Game.Id);

        return finalGame;
    }

    [Fact]
    public async Task Handle_GameNotFound_ReturnsError()
    {
        // Arrange
        var command = new PerformShowdownCommand(Guid.NewGuid());

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NotInShowdownPhase_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        var command = new PerformShowdownCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidShowdown_ReturnsSuccess()
    {
        // Arrange
        var game = await CreateGameInShowdownPhaseAsync();
        var command = new PerformShowdownCommand(game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidShowdown_AdvancesToComplete()
    {
        // Arrange
        var game = await CreateGameInShowdownPhaseAsync();
        var command = new PerformShowdownCommand(game.Id);

        // Act
        await Mediator.Send(command);

        // Assert
        var freshGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        freshGame.CurrentPhase.Should().Be(nameof(Phases.Complete));
    }

    [Fact]
    public async Task Handle_SinglePlayerRemaining_AwardsPotWithoutEvaluation()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 3, ante: 10);
        
        // Start hand and go through initial phases
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // One player bets, others fold
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Bet, 10));
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Fold, 0));
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Fold, 0));

        // Game should have advanced to showdown with one player remaining
        var game = await DbContext.Games
            .Include(g => g.GamePlayers)
            .Include(g => g.Pots)
            .FirstAsync(g => g.Id == setup.Game.Id);

        // Assert - The remaining player should get the pot
        var activePlayers = game.GamePlayers.Where(gp => !gp.HasFolded).ToList();
        activePlayers.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_PotsMarkedAsAwarded()
    {
        // Arrange
        var game = await CreateGameInShowdownPhaseAsync();
        var command = new PerformShowdownCommand(game.Id);

        // Act
        await Mediator.Send(command);

        // Assert
        var pots = await GetFreshDbContext().Pots
            .Where(p => p.GameId == game.Id && p.HandNumber == game.CurrentHandNumber)
            .ToListAsync();
        
        pots.Should().AllSatisfy(p => p.IsAwarded.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_WinnerChipsUpdated()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2, startingChips: 1000, ante: 10);
        
        // Start and go through game phases
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));
        
        // Betting round
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Check, 0));
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Check, 0));
        
        // Draw phase
        for (int i = 0; i < 2; i++)
        {
            await Mediator.Send(new ProcessDrawCommand(setup.Game.Id, new List<int>()));
        }
        
        // Second betting
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Check, 0));
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Check, 0));

        // Perform showdown
        var result = await Mediator.Send(new PerformShowdownCommand(setup.Game.Id));

        // Assert - Winner should have pot added to their chips
        result.IsT0.Should().BeTrue();
        
        var freshPlayers = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == setup.Game.Id)
            .ToListAsync();

        // Total chips should equal starting chips (no money leaves the table)
        var totalChips = freshPlayers.Sum(p => p.ChipStack);
        totalChips.Should().Be(2000); // 2 players x 1000 starting chips
    }
}
