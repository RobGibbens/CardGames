using CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;
using CardGames.Poker.Api.Games;

namespace CardGames.IntegrationTests.Features.Commands;

/// <summary>
/// Integration tests for <see cref="CreateGameCommandHandler"/>.
/// Tests game creation scenarios including validation, player setup, and pot initialization.
/// </summary>
public class CreateGameCommandHandlerTests : IntegrationTestBase
{
    [Theory]
    [InlineData(PokerGameMetadataRegistry.FiveCardDrawCode)]
    [InlineData(PokerGameMetadataRegistry.SevenCardStudCode)]
    [InlineData(PokerGameMetadataRegistry.KingsAndLowsCode)]
    [InlineData(PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode)]
    [InlineData(PokerGameMetadataRegistry.HoldEmCode)]
    public async Task Handle_ValidRequest_CreatesGame(string gameCode)
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            gameCode,
            "Test Game",
            10,
            20,
            new List<PlayerInfo>
            {
                new("Player1", 1000),
                new("Player2", 1000)
            });

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue("Expected successful creation");
        var success = result.AsT0;
        success.GameId.Should().Be(gameId);
        success.GameTypeCode.Should().Be(gameCode);
        success.PlayerCount.Should().Be(2);

        // Verify game in database
        var game = await DbContext.Games
            .Include(g => g.GameType)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        game.Should().NotBeNull();
        game!.Name.Should().Be("Test Game");
        game.Ante.Should().Be(10);
        game.MinBet.Should().Be(20);
        game.Status.Should().Be(GameStatus.WaitingForPlayers);
        game.CurrentPhase.Should().Be(nameof(Phases.WaitingToStart));
    }

    [Fact]
    public async Task Handle_EmptyGameId_ReturnsConflict()
    {
        // Arrange
        var command = new CreateGameCommand(
            Guid.Empty,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Test Game",
            10,
            20,
            new List<PlayerInfo> { new("Player1", 1000) });

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue("Expected conflict result");
        result.AsT1.Reason.Should().Contain("non-empty GUID");
    }

    [Fact]
    public async Task Handle_DuplicateGameId_ReturnsConflict()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command1 = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "First Game",
            10,
            20,
            new List<PlayerInfo> { new("Player1", 1000) });
        
        await Mediator.Send(command1);

        var command2 = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Second Game",
            10,
            20,
            new List<PlayerInfo> { new("Player2", 1000) });

        // Act
        var result = await Mediator.Send(command2);

        // Assert
        result.IsT1.Should().BeTrue("Expected conflict result");
        result.AsT1.Reason.Should().Contain("already exists");
    }

    [Fact]
    public async Task Handle_UnknownGameCode_ReturnsConflict()
    {
        // Arrange
        var command = new CreateGameCommand(
            Guid.NewGuid(),
            "UNKNOWNGAME",
            "Test Game",
            10,
            20,
            new List<PlayerInfo> { new("Player1", 1000) });

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue("Expected conflict result");
        result.AsT1.Reason.Should().Contain("Unknown game code");
    }

    [Fact]
    public async Task Handle_CreatesPlayersWithCorrectChipStacks()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Test Game",
            10,
            20,
            new List<PlayerInfo>
            {
                new("Player1", 500),
                new("Player2", 750),
                new("Player3", 1000)
            });

        // Act
        await Mediator.Send(command);

        // Assert
        var gamePlayers = await DbContext.GamePlayers
            .Where(gp => gp.GameId == gameId)
            .OrderBy(gp => gp.SeatPosition)
            .ToListAsync();

        gamePlayers.Should().HaveCount(3);
        gamePlayers[0].ChipStack.Should().Be(500);
        gamePlayers[0].SeatPosition.Should().Be(0);
        gamePlayers[1].ChipStack.Should().Be(750);
        gamePlayers[1].SeatPosition.Should().Be(1);
        gamePlayers[2].ChipStack.Should().Be(1000);
        gamePlayers[2].SeatPosition.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CreatesInitialPot()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Test Game",
            10,
            20,
            new List<PlayerInfo> { new("Player1", 1000), new("Player2", 1000) });

        // Act
        await Mediator.Send(command);

        // Assert
        var pots = await DbContext.Pots
            .Where(p => p.GameId == gameId)
            .ToListAsync();

        pots.Should().HaveCount(1);
        pots[0].PotType.Should().Be(PotType.Main);
        pots[0].Amount.Should().Be(0);
        pots[0].HandNumber.Should().Be(1);
        pots[0].IsAwarded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReusesExistingPlayer()
    {
        // Arrange
        var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "ExistingPlayer");
        
        var command = new CreateGameCommand(
            Guid.NewGuid(),
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Test Game",
            10,
            20,
            new List<PlayerInfo> { new("ExistingPlayer", 1000) });

        // Act
        await Mediator.Send(command);

        // Assert - Should reuse existing player, not create duplicate
        var players = await DbContext.Players
            .Where(p => p.Name == "ExistingPlayer")
            .ToListAsync();
        
        players.Should().HaveCount(1);
        players[0].Id.Should().Be(player.Id);
    }

    [Fact]
    public async Task Handle_CreatesGameType_WhenNotExists()
    {
        // Arrange
        // Clear the game types from seed data to test creation
        var existingTypes = await DbContext.GameTypes.ToListAsync();
        DbContext.GameTypes.RemoveRange(existingTypes);
        await DbContext.SaveChangesAsync();

        var command = new CreateGameCommand(
            Guid.NewGuid(),
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Test Game",
            10,
            20,
            new List<PlayerInfo> { new("Player1", 1000), new("Player2", 1000) });

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        
        var gameType = await DbContext.GameTypes
            .FirstOrDefaultAsync(gt => gt.Code == PokerGameMetadataRegistry.FiveCardDrawCode);
        gameType.Should().NotBeNull();
        gameType!.Name.Should().Be("Five Card Draw");
    }

    [Fact]
    public async Task Handle_SetsCorrectPlayerStatus()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Test Game",
            10,
            20,
            new List<PlayerInfo> { new("Player1", 1000), new("Player2", 1000) });

        // Act
        await Mediator.Send(command);

        // Assert
        var gamePlayers = await DbContext.GamePlayers
            .Where(gp => gp.GameId == gameId)
            .ToListAsync();

        gamePlayers.Should().AllSatisfy(gp =>
        {
            gp.Status.Should().Be(GamePlayerStatus.Active);
            gp.HasFolded.Should().BeFalse();
            gp.IsAllIn.Should().BeFalse();
            gp.IsSittingOut.Should().BeFalse();
            gp.IsConnected.Should().BeTrue();
        });
    }

    [Fact]
    public async Task Handle_SetsGameDealerPositionToZero()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Test Game",
            10,
            20,
            new List<PlayerInfo> { new("Player1", 1000), new("Player2", 1000) });

        // Act
        await Mediator.Send(command);

        // Assert
        var game = await DbContext.Games.FirstAsync(g => g.Id == gameId);
        game.DealerPosition.Should().Be(0);
        game.CurrentPlayerIndex.Should().Be(-1);
        game.CurrentDrawPlayerIndex.Should().Be(-1);
    }

    [Fact]
    public async Task Handle_NullGameName_CreatesGameWithDefaultName()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            null,
            10,
            20,
            new List<PlayerInfo> { new("Player1", 1000), new("Player2", 1000) });

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var game = await DbContext.Games.FirstAsync(g => g.Id == gameId);
        game.Name.Should().BeNull();
    }
}
