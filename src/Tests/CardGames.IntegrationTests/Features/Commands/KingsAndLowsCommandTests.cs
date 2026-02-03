using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.StartHand;

namespace CardGames.IntegrationTests.Features.Commands;

/// <summary>
/// Integration tests for Kings and Lows specific commands.
/// Tests DropOrStay decision handling.
/// </summary>
public class KingsAndLowsCommandTests : IntegrationTestBase
{
    private async Task<(GameSetup Setup, Game Game)> CreateKingsAndLowsGameInDropOrStayPhaseAsync()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        
        // Start hand - Kings and Lows starts with Dealing then moves to DropOrStay
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        
        // Reload game with cards
        var game = await DbContext.Games
            .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
            .Include(g => g.GameCards)
            .FirstAsync(g => g.Id == setup.Game.Id);

        // Should now be in DropOrStay phase
        game.CurrentPhase.Should().Be(nameof(Phases.DropOrStay));

        return (setup, game);
    }

    [Fact]
    public async Task StartHand_KingsAndLows_TransitionsToDropOrStay()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);

        // Act
        var result = await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Assert
        result.IsT0.Should().BeTrue();
        
        var game = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
        // After dealing, should be in DropOrStay
        game.CurrentPhase.Should().Be(nameof(Phases.DropOrStay));
    }

    [Fact]
    public async Task DropOrStay_StayDecision_MarksPlayerAsStayed()
    {
        // Arrange
        var (setup, game) = await CreateKingsAndLowsGameInDropOrStayPhaseAsync();
        var player = setup.GamePlayers[0];

        var command = new DropOrStayCommand(game.Id, player.PlayerId, "Stay");

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();

        var updatedPlayer = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.Id == player.Id);
        updatedPlayer.DropOrStayDecision.Should().Be(Data.Entities.DropOrStayDecision.Stay);
        updatedPlayer.HasFolded.Should().BeFalse();
    }

    [Fact]
    public async Task DropOrStay_DropDecision_MarksPlayerAsDropped()
    {
        // Arrange
        var (setup, game) = await CreateKingsAndLowsGameInDropOrStayPhaseAsync();
        var player = setup.GamePlayers[0];

        var command = new DropOrStayCommand(game.Id, player.PlayerId, "Drop");

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();

        var updatedPlayer = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.Id == player.Id);
        updatedPlayer.DropOrStayDecision.Should().Be(Data.Entities.DropOrStayDecision.Drop);
        updatedPlayer.HasFolded.Should().BeTrue();
    }

    [Fact]
    public async Task DropOrStay_GameNotFound_ReturnsError()
    {
        // Arrange
        var command = new DropOrStayCommand(Guid.NewGuid(), Guid.NewGuid(), "Stay");

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
    }

    [Fact]
    public async Task DropOrStay_WrongPhase_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        // Don't start hand - game is still in WaitingToStart phase
        
        var command = new DropOrStayCommand(setup.Game.Id, setup.Players[0].Id, "Stay");

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
    }

    [Fact]
    public async Task DropOrStay_AllPlayersDecide_AdvancesPhase()
    {
        // Arrange
        var (setup, game) = await CreateKingsAndLowsGameInDropOrStayPhaseAsync();

        // All players stay
        foreach (var player in setup.GamePlayers)
        {
            await Mediator.Send(new DropOrStayCommand(game.Id, player.PlayerId, "Stay"));
        }

        // Assert - Should have advanced past DropOrStay phase
        var finalGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        finalGame.CurrentPhase.Should().NotBe(nameof(Phases.DropOrStay));
    }

    [Fact]
    public async Task DropOrStay_AllButOneDrop_GoesToPlayerVsDeck()
    {
        // Arrange
        var (setup, game) = await CreateKingsAndLowsGameInDropOrStayPhaseAsync();

        // First player stays
        await Mediator.Send(new DropOrStayCommand(game.Id, setup.GamePlayers[0].PlayerId, "Stay"));

        // Rest drop
        for (int i = 1; i < setup.GamePlayers.Count; i++)
        {
            await Mediator.Send(new DropOrStayCommand(game.Id, setup.GamePlayers[i].PlayerId, "Drop"));
        }

        // Assert - Should go to PlayerVsDeck phase
        var finalGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        finalGame.CurrentPhase.Should().Be(nameof(Phases.PlayerVsDeck));
    }

    [Fact]
    public async Task DropOrStay_AllDrop_GoesToComplete()
    {
        // Arrange
        var (setup, game) = await CreateKingsAndLowsGameInDropOrStayPhaseAsync();

        // All players drop
        foreach (var player in setup.GamePlayers)
        {
            await Mediator.Send(new DropOrStayCommand(game.Id, player.PlayerId, "Drop"));
        }

        // Assert - Should complete the hand
        var finalGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        finalGame.CurrentPhase.Should().Be(nameof(Phases.Complete));
    }

    [Fact]
    public async Task DropOrStay_MultiplePlayersStay_GoesToDrawPhase()
    {
        // Arrange
        var (setup, game) = await CreateKingsAndLowsGameInDropOrStayPhaseAsync();

        // Two players stay, two drop
        await Mediator.Send(new DropOrStayCommand(game.Id, setup.GamePlayers[0].PlayerId, "Stay"));
        await Mediator.Send(new DropOrStayCommand(game.Id, setup.GamePlayers[1].PlayerId, "Stay"));
        await Mediator.Send(new DropOrStayCommand(game.Id, setup.GamePlayers[2].PlayerId, "Drop"));
        await Mediator.Send(new DropOrStayCommand(game.Id, setup.GamePlayers[3].PlayerId, "Drop"));

        // Assert - Should go to DrawPhase
        var finalGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        finalGame.CurrentPhase.Should().Be(nameof(Phases.DrawPhase));
    }

    [Fact]
    public async Task StartHand_ResetsDropOrStayDecisions()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        
        // Set previous decisions
        foreach (var gp in setup.GamePlayers)
        {
            gp.DropOrStayDecision = Data.Entities.DropOrStayDecision.Stay;
        }
        await DbContext.SaveChangesAsync();

        // Act - Start new hand
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Assert - Decisions should be reset
        var players = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == setup.Game.Id)
            .ToListAsync();

        players.Should().AllSatisfy(gp => gp.DropOrStayDecision.Should().Be(Data.Entities.DropOrStayDecision.Undecided));
    }
}
