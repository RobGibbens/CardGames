using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Features.Commands;

/// <summary>
/// Integration tests for <see cref="ProcessDrawCommandHandler"/>.
/// Tests draw action processing including discards and replacements.
/// </summary>
public class ProcessDrawCommandHandlerTests : IntegrationTestBase
{
    private async Task<(GameSetup Setup, Game Game, Guid CurrentDrawPlayerId)> CreateGameInDrawPhaseAsync()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4, ante: 10);
        
        // Start hand
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        
        // Collect antes
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
        
        // Deal hands
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // Complete first betting round (everyone checks)
        for (int i = 0; i < 4; i++)
        {
            await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingActionType.Check, 0));
        }

        // Reload game
        var game = await DbContext.Games
            .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
            .Include(g => g.GameCards)
            .FirstAsync(g => g.Id == setup.Game.Id);

        // Game should now be in DrawPhase
        game.CurrentPhase.Should().Be(nameof(Phases.DrawPhase));
        
        var currentDrawPlayer = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentDrawPlayerIndex);
        
        return (setup, game, currentDrawPlayer.PlayerId);
    }

    [Fact]
    public async Task Handle_GameNotFound_ReturnsError()
    {
        // Arrange
        var command = new ProcessDrawCommand(
            Guid.NewGuid(),
            new List<int>());

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(ProcessDrawErrorCode.GameNotFound);
    }

    [Fact]
    public async Task Handle_NotInDrawPhase_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var command = new ProcessDrawCommand(
            setup.Game.Id,
            new List<int>());

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(ProcessDrawErrorCode.NotInDrawPhase);
    }

    [Fact]
    public async Task Handle_StandPat_NoDiscards()
    {
        // Arrange
        var (setup, game, currentPlayerId) = await CreateGameInDrawPhaseAsync();
        var command = new ProcessDrawCommand(
            game.Id,
            new List<int>()); // Empty list = stand pat

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.DiscardedCards.Count.Should().Be(0);
        success.NewCards.Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DiscardOne_ReceivesOneCard()
    {
        // Arrange
        var (setup, game, currentPlayerId) = await CreateGameInDrawPhaseAsync();
        var command = new ProcessDrawCommand(
            game.Id,
            new List<int> { 0 }); // Discard first card

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.DiscardedCards.Count.Should().Be(1);
        success.NewCards.Count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DiscardThree_ReceivesThreeCards()
    {
        // Arrange
        var (setup, game, currentPlayerId) = await CreateGameInDrawPhaseAsync();
        var command = new ProcessDrawCommand(
            game.Id,
            new List<int> { 0, 1, 2 }); // Discard three cards

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.DiscardedCards.Count.Should().Be(3);
        success.NewCards.Count.Should().Be(3);
    }

    [Fact]
    public async Task Handle_TooManyDiscards_ReturnsError()
    {
        // Arrange
        var (setup, game, currentPlayerId) = await CreateGameInDrawPhaseAsync();
        var command = new ProcessDrawCommand(
            game.Id,
            new List<int> { 0, 1, 2, 3, 4 }); // Discard all 5 cards (max is usually 3)

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(ProcessDrawErrorCode.TooManyDiscards);
    }

    [Fact]
    public async Task Handle_InvalidCardIndex_ReturnsError()
    {
        // Arrange
        var (setup, game, currentPlayerId) = await CreateGameInDrawPhaseAsync();
        var command = new ProcessDrawCommand(
            game.Id,
            new List<int> { 10 }); // Invalid index

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(ProcessDrawErrorCode.InvalidCardIndex);
    }

    [Fact]
    public async Task Handle_NegativeCardIndex_ReturnsError()
    {
        // Arrange
        var (setup, game, currentPlayerId) = await CreateGameInDrawPhaseAsync();
        var command = new ProcessDrawCommand(
            game.Id,
            new List<int> { -1 }); // Negative index

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(ProcessDrawErrorCode.InvalidCardIndex);
    }

    [Fact]
    public async Task Handle_MarksDiscardedCardsCorrectly()
    {
        // Arrange
        var (setup, game, currentPlayerId) = await CreateGameInDrawPhaseAsync();
        var currentPlayer = game.GamePlayers.First(gp => gp.PlayerId == currentPlayerId);
        
        // Get the cards before draw
        var cardsBefore = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id && gc.GamePlayerId == currentPlayer.Id && !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .ToListAsync();

        var command = new ProcessDrawCommand(
            game.Id,
            new List<int> { 0, 2 }); // Discard 1st and 3rd cards

        // Act
        await Mediator.Send(command);

        // Assert
        var freshContext = GetFreshDbContext();
        var discardedCards = await freshContext.GameCards
            .Where(gc => gc.GameId == game.Id && gc.GamePlayerId == currentPlayer.Id && gc.IsDiscarded)
            .ToListAsync();

        discardedCards.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_NewCardsAreUnique()
    {
        // Arrange
        var (setup, game, currentPlayerId) = await CreateGameInDrawPhaseAsync();
        var currentPlayer = game.GamePlayers.First(gp => gp.PlayerId == currentPlayerId);

        var command = new ProcessDrawCommand(
            game.Id,
            new List<int> { 0, 1, 2 }); // Discard 3 cards

        // Act
        await Mediator.Send(command);

        // Assert - All cards should be unique
        var freshContext = GetFreshDbContext();
        var allPlayerCards = await freshContext.GameCards
            .Where(gc => gc.GameId == game.Id && gc.GamePlayerId == currentPlayer.Id && !gc.IsDiscarded)
            .ToListAsync();

        var uniqueCards = allPlayerCards
            .Select(c => $"{c.Suit}-{c.Symbol}")
            .Distinct()
            .ToList();

        allPlayerCards.Should().HaveCount(5); // Still 5 cards after draw
        uniqueCards.Should().HaveCount(5);
    }

    [Fact]
    public async Task Handle_AdvancesToNextPlayer()
    {
        // Arrange
        var (setup, game, currentPlayerId) = await CreateGameInDrawPhaseAsync();
        var initialDrawIndex = game.CurrentDrawPlayerIndex;

        var command = new ProcessDrawCommand(
            game.Id,
            new List<int>());

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        
        // Next player should be different
        if (!success.DrawComplete)
        {
            success.NextDrawPlayerIndex.Should().NotBe(initialDrawIndex);
        }
    }

    [Fact]
    public async Task Handle_LastPlayerCompletes_AdvancesPhase()
    {
        // Arrange
        var (setup, game, _) = await CreateGameInDrawPhaseAsync();
        
        // Process draws for all players
        for (int i = 0; i < 4; i++)
        {
            var freshGame = await DbContext.Games
                .Include(g => g.GamePlayers)
                .FirstAsync(g => g.Id == game.Id);
            
            var currentPlayer = freshGame.GamePlayers.First(gp => gp.SeatPosition == freshGame.CurrentDrawPlayerIndex);
            
            await Mediator.Send(new ProcessDrawCommand(
                game.Id,
                new List<int>()));
        }

        // Assert - Should have advanced past draw phase
        var finalGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        finalGame.CurrentPhase.Should().NotBe(nameof(Phases.DrawPhase));
    }

    [Fact]
    public async Task Handle_MarksPlayerAsHasDrawnThisRound()
    {
        // Arrange
        var (setup, game, currentPlayerId) = await CreateGameInDrawPhaseAsync();
        var currentPlayer = game.GamePlayers.First(gp => gp.PlayerId == currentPlayerId);

        var command = new ProcessDrawCommand(
            game.Id,
            new List<int>());

        // Act
        await Mediator.Send(command);

        // Assert
        var updatedPlayer = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.Id == currentPlayer.Id);
        updatedPlayer.HasDrawnThisRound.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NewCardsMarkedAsDrawn()
    {
        // Arrange
        var (setup, game, currentPlayerId) = await CreateGameInDrawPhaseAsync();
        var currentPlayer = game.GamePlayers.First(gp => gp.PlayerId == currentPlayerId);

        var command = new ProcessDrawCommand(
            game.Id,
            new List<int> { 0, 1 });

        // Act
        await Mediator.Send(command);

        // Assert
        var drawnCards = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == game.Id && gc.GamePlayerId == currentPlayer.Id && gc.IsDrawnCard)
            .ToListAsync();

        drawnCards.Should().HaveCount(2);
        drawnCards.Should().AllSatisfy(c => c.DrawnAtRound.Should().Be(1));
    }
}
