using CardGames.Poker.Api.Features.Games.Common.v1.Commands.ChooseDealerGame;
using CardGames.Poker.Api.Games;
using CardGames.IntegrationTests.Infrastructure.Fakes;

namespace CardGames.IntegrationTests.Features.Commands;

/// <summary>
/// Integration tests for <see cref="ChooseDealerGameCommandHandler"/>.
/// Tests game type selection in Dealer's Choice mode.
/// </summary>
public class ChooseDealerGameCommandTests : IntegrationTestBase
{
    [Fact]
    public async Task Handle_ValidChoice_SetsGameTypeAndTransitionsToWaitingToStart()
    {
        // Arrange — DC game with "Test User" at seat 0 as the DC dealer
        var setup = await CreateDealersChoiceSetupWithTestUser(dealerSeatPosition: 0);

        var command = new ChooseDealerGameCommand(
            setup.Game.Id,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            Ante: 10,
            MinBet: 20);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue("Expected successful choice");
        var success = result.AsT0;
        success.GameId.Should().Be(setup.Game.Id);
        success.GameTypeCode.Should().Be(PokerGameMetadataRegistry.FiveCardDrawCode);
        success.GameTypeName.Should().Be("Five Card Draw");
        success.Ante.Should().Be(10);
        success.MinBet.Should().Be(20);

        // Verify DB state
        var game = await DbContext.Games
            .Include(g => g.GameType)
            .FirstAsync(g => g.Id == setup.Game.Id);
        game.GameTypeId.Should().NotBeNull();
        game.CurrentHandGameTypeCode.Should().Be(PokerGameMetadataRegistry.FiveCardDrawCode);
        game.Ante.Should().Be(10);
        game.MinBet.Should().Be(20);
        game.CurrentPhase.Should().Be(nameof(Phases.WaitingToStart));
        game.NextHandStartsAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_GameNotFound_ReturnsError()
    {
        var command = new ChooseDealerGameCommand(
            Guid.NewGuid(),
            PokerGameMetadataRegistry.FiveCardDrawCode,
            Ante: 10,
            MinBet: 20);

        var result = await Mediator.Send(command);

        result.IsT1.Should().BeTrue();
        result.AsT1.Reason.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_NotDealersChoiceTable_ReturnsError()
    {
        // Arrange — create a standard (non-DC) game
        var game = await DatabaseSeeder.CreateGameAsync(DbContext, PokerGameMetadataRegistry.FiveCardDrawCode);
        game.CurrentPhase = nameof(Phases.WaitingForDealerChoice);
        await DbContext.SaveChangesAsync();

        var command = new ChooseDealerGameCommand(
            game.Id,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            Ante: 10,
            MinBet: 20);

        var result = await Mediator.Send(command);

        result.IsT1.Should().BeTrue();
        result.AsT1.Reason.Should().Contain("not a Dealer's Choice");
    }

    [Fact]
    public async Task Handle_WrongPhase_ReturnsError()
    {
        // Arrange — DC game but in WaitingToStart phase (not WaitingForDealerChoice)
        var setup = await CreateDealersChoiceSetupWithTestUser(dealerSeatPosition: 0);
        setup.Game.CurrentPhase = nameof(Phases.WaitingToStart);
        await DbContext.SaveChangesAsync();

        var command = new ChooseDealerGameCommand(
            setup.Game.Id,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            Ante: 10,
            MinBet: 20);

        var result = await Mediator.Send(command);

        result.IsT1.Should().BeTrue();
        result.AsT1.Reason.Should().Contain("not waiting for a dealer choice");
    }

    [Fact]
    public async Task Handle_NonDealerPlayer_ReturnsError()
    {
        // Arrange — DC game where "Test User" is at seat 1, but DC dealer is at seat 0
        var setup = await CreateDealersChoiceSetupWithTestUser(
            dealerSeatPosition: 0,
            testUserSeatPosition: 1);

        var command = new ChooseDealerGameCommand(
            setup.Game.Id,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            Ante: 10,
            MinBet: 20);

        var result = await Mediator.Send(command);

        result.IsT1.Should().BeTrue();
        result.AsT1.Reason.Should().Contain("Only the current dealer");
    }

    [Fact]
    public async Task Handle_UnknownGameTypeCode_ReturnsError()
    {
        var setup = await CreateDealersChoiceSetupWithTestUser(dealerSeatPosition: 0);

        var command = new ChooseDealerGameCommand(
            setup.Game.Id,
            "NOTAREALGAME",
            Ante: 10,
            MinBet: 20);

        var result = await Mediator.Send(command);

        result.IsT1.Should().BeTrue();
        result.AsT1.Reason.Should().Contain("Unknown game type code");
    }

    [Fact]
    public async Task Handle_NegativeAnte_ReturnsError()
    {
        var setup = await CreateDealersChoiceSetupWithTestUser(dealerSeatPosition: 0);

        var command = new ChooseDealerGameCommand(
            setup.Game.Id,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            Ante: -5,
            MinBet: 20);

        var result = await Mediator.Send(command);

        result.IsT1.Should().BeTrue();
        result.AsT1.Reason.Should().Contain("Ante must be zero or greater");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Handle_ZeroOrNegativeMinBet_ReturnsError(int minBet)
    {
        var setup = await CreateDealersChoiceSetupWithTestUser(dealerSeatPosition: 0);

        var command = new ChooseDealerGameCommand(
            setup.Game.Id,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            Ante: 10,
            MinBet: minBet);

        var result = await Mediator.Send(command);

        result.IsT1.Should().BeTrue();
        result.AsT1.Reason.Should().Contain("Minimum bet must be greater than zero");
    }

    [Fact]
    public async Task Handle_ZeroAnte_Succeeds()
    {
        var setup = await CreateDealersChoiceSetupWithTestUser(dealerSeatPosition: 0);

        var command = new ChooseDealerGameCommand(
            setup.Game.Id,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            Ante: 0,
            MinBet: 10);

        var result = await Mediator.Send(command);

        result.IsT0.Should().BeTrue("Zero ante should be allowed");
        result.AsT0.Ante.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CreatesDealersChoiceHandLog()
    {
        var setup = await CreateDealersChoiceSetupWithTestUser(dealerSeatPosition: 0);

        var command = new ChooseDealerGameCommand(
            setup.Game.Id,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            Ante: 10,
            MinBet: 20);

        await Mediator.Send(command);

        var log = await DbContext.DealersChoiceHandLogs
            .FirstOrDefaultAsync(l => l.GameId == setup.Game.Id);
        log.Should().NotBeNull();
        log!.GameTypeCode.Should().Be(PokerGameMetadataRegistry.FiveCardDrawCode);
        log.GameTypeName.Should().Be("Five Card Draw");
        log.Ante.Should().Be(10);
        log.MinBet.Should().Be(20);
        log.DealerSeatPosition.Should().Be(0);
    }

    [Theory]
    [InlineData(PokerGameMetadataRegistry.HoldEmCode, 5, 10)]
    [InlineData(PokerGameMetadataRegistry.OmahaCode, 10, 20)]
    public async Task Handle_BlindBasedChoice_SetsAndLogsBlinds(
        string gameTypeCode,
        int smallBlind,
        int bigBlind)
    {
        // Arrange
        var setup = await CreateDealersChoiceSetupWithTestUser(dealerSeatPosition: 0);

        var command = new ChooseDealerGameCommand(
            setup.Game.Id,
            gameTypeCode,
            Ante: 0,
            MinBet: 10,
            SmallBlind: smallBlind,
            BigBlind: bigBlind);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue($"Expected success for blind-based game {gameTypeCode}");
        result.AsT0.GameTypeCode.Should().Be(gameTypeCode);
        result.AsT0.SmallBlind.Should().Be(smallBlind);
        result.AsT0.BigBlind.Should().Be(bigBlind);

        var game = await DbContext.Games
            .Include(g => g.GameType)
            .FirstAsync(g => g.Id == setup.Game.Id);

        game.CurrentHandGameTypeCode.Should().Be(gameTypeCode);
        game.SmallBlind.Should().Be(smallBlind);
        game.BigBlind.Should().Be(bigBlind);

        var handLog = await DbContext.DealersChoiceHandLogs
            .SingleAsync(l => l.GameId == setup.Game.Id);

        handLog.GameTypeCode.Should().Be(gameTypeCode);
        handLog.SmallBlind.Should().Be(smallBlind);
        handLog.BigBlind.Should().Be(bigBlind);
    }

    [Theory]
    [InlineData(PokerGameMetadataRegistry.FiveCardDrawCode)]
    [InlineData(PokerGameMetadataRegistry.SevenCardStudCode)]
    [InlineData(PokerGameMetadataRegistry.HoldEmCode)]
    [InlineData(PokerGameMetadataRegistry.OmahaCode)]
    [InlineData(PokerGameMetadataRegistry.KingsAndLowsCode)]
    public async Task Handle_DifferentGameTypes_AllSucceed(string gameTypeCode)
    {
        var setup = await CreateDealersChoiceSetupWithTestUser(dealerSeatPosition: 0);

        var command = new ChooseDealerGameCommand(
            setup.Game.Id,
            gameTypeCode,
            Ante: 10,
            MinBet: 20);

        var result = await Mediator.Send(command);

        result.IsT0.Should().BeTrue($"Expected success for {gameTypeCode}");
        result.AsT0.GameTypeCode.Should().Be(gameTypeCode);
    }

    [Fact]
    public async Task Handle_NullDealersChoiceDealerPosition_ReturnsError()
    {
        // Arrange — DC game with null DealersChoiceDealerPosition
        var game = await DatabaseSeeder.CreateDealersChoiceGameAsync(DbContext);
        var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, "Test User");
        await DatabaseSeeder.AddPlayerToGameAsync(DbContext, game, player, 0);

        game.CurrentPhase = nameof(Phases.WaitingForDealerChoice);
        game.DealersChoiceDealerPosition = null;
        game.CurrentHandNumber = 1;
        game.Status = GameStatus.InProgress;
        await DbContext.SaveChangesAsync();

        var command = new ChooseDealerGameCommand(
            game.Id,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            Ante: 10,
            MinBet: 20);

        var result = await Mediator.Send(command);

        result.IsT1.Should().BeTrue();
        result.AsT1.Reason.Should().Contain("dealer position is not set");
    }

    /// <summary>
    /// Creates a DC game setup where "Test User" (FakeCurrentUserService.UserName)
    /// is at the specified seat, ready for WaitingForDealerChoice.
    /// </summary>
    private async Task<GameSetup> CreateDealersChoiceSetupWithTestUser(
        int dealerSeatPosition,
        int testUserSeatPosition = -1,
        int playerCount = 3)
    {
        // If testUserSeatPosition not specified, place "Test User" at the dealer seat
        if (testUserSeatPosition < 0)
            testUserSeatPosition = dealerSeatPosition;

        var game = await DatabaseSeeder.CreateDealersChoiceGameAsync(DbContext);
        var players = new List<Player>();
        var gamePlayers = new List<GamePlayer>();

        for (var i = 0; i < playerCount; i++)
        {
            var playerName = i == testUserSeatPosition ? "Test User" : $"Player {i + 1}";
            var player = await DatabaseSeeder.CreatePlayerAsync(DbContext, playerName);
            var gamePlayer = await DatabaseSeeder.AddPlayerToGameAsync(DbContext, game, player, i);
            players.Add(player);
            gamePlayers.Add(gamePlayer);
        }

        game.DealersChoiceDealerPosition = dealerSeatPosition;
        game.CurrentPhase = nameof(Phases.WaitingForDealerChoice);
        game.CurrentHandNumber = 1;
        game.Status = GameStatus.InProgress;
        await DbContext.SaveChangesAsync();

        var loadedGame = await DbContext.Games
            .Include(g => g.GamePlayers)
                .ThenInclude(gp => gp.Player)
            .FirstAsync(g => g.Id == game.Id);

        return new GameSetup(loadedGame, players, gamePlayers);
    }
}
