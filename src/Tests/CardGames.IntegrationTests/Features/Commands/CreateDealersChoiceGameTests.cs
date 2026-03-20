using System.Text.Json;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;
using CardGames.Poker.Api.Games;

namespace CardGames.IntegrationTests.Features.Commands;

/// <summary>
/// Integration tests for CreateGameCommandHandler in Dealer's Choice mode.
/// </summary>
public class CreateDealersChoiceGameTests : IntegrationTestBase
{
    [Fact]
    public async Task Handle_DealersChoice_CreatesGameWithNullGameType()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            string.Empty,
            "DC Table",
            10,
            20,
            new List<PlayerInfo>
            {
                new("Player1", 1000),
                new("Player2", 1000)
            },
            IsDealersChoice: true);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue("Expected successful creation");

        var game = await DbContext.Games
            .Include(g => g.GameType)
            .FirstOrDefaultAsync(g => g.Id == gameId);
        game.Should().NotBeNull();
        game!.IsDealersChoice.Should().BeTrue();
        game.GameTypeId.Should().BeNull();
        game.GameType.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DealersChoice_SetsAnteAndMinBetToNull()
    {
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            string.Empty,
            "DC Table",
            10,
            20,
            new List<PlayerInfo> { new("Player1", 1000), new("Player2", 1000) },
            IsDealersChoice: true);

        await Mediator.Send(command);

        var game = await DbContext.Games.FirstAsync(g => g.Id == gameId);
        game.Ante.Should().BeNull("DC games set ante per-hand");
        game.MinBet.Should().BeNull("DC games set min bet per-hand");
    }

    [Fact]
    public async Task Handle_DealersChoice_PlayersCreatedNormally()
    {
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            string.Empty,
            "DC Table",
            0,
            0,
            new List<PlayerInfo>
            {
                new("Player1", 500),
                new("Player2", 750),
                new("Player3", 1000)
            },
            IsDealersChoice: true);

        await Mediator.Send(command);

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
    public async Task Handle_DealersChoice_SetsPhaseToWaitingToStart()
    {
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            string.Empty,
            "DC Table",
            0,
            0,
            new List<PlayerInfo> { new("P1", 1000), new("P2", 1000) },
            IsDealersChoice: true);

        await Mediator.Send(command);

        var game = await DbContext.Games.FirstAsync(g => g.Id == gameId);
        game.CurrentPhase.Should().Be(nameof(Phases.WaitingToStart));
        game.Status.Should().Be(GameStatus.WaitingForPlayers);
    }

    [Fact]
    public async Task Handle_DealersChoice_CreatesInitialPot()
    {
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            string.Empty,
            "DC Table",
            0,
            0,
            new List<PlayerInfo> { new("P1", 1000), new("P2", 1000) },
            IsDealersChoice: true);

        await Mediator.Send(command);

        var pots = await DbContext.Pots
            .Where(p => p.GameId == gameId)
            .ToListAsync();

        pots.Should().HaveCount(1);
        pots[0].PotType.Should().Be(PotType.Main);
        pots[0].Amount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DealersChoice_PersistsAllowedGameCodesInGameSettings()
    {
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            string.Empty,
            "DC Table",
            0,
            0,
            new List<PlayerInfo> { new("P1", 1000), new("P2", 1000) },
            IsDealersChoice: true)
        {
            AllowedDealerChoiceGameCodes =
            [
                PokerGameMetadataRegistry.FiveCardDrawCode,
                PokerGameMetadataRegistry.OmahaCode
            ]
        };

        await Mediator.Send(command);

        var game = await DbContext.Games.FirstAsync(g => g.Id == gameId);
        game.GameSettings.Should().NotBeNullOrWhiteSpace();

        using var document = JsonDocument.Parse(game.GameSettings!);
        var allowedGameCodes = document.RootElement
            .GetProperty("allowedDealerChoiceGameCodes")
            .EnumerateArray()
            .Select(element => element.GetString())
            .Where(code => code is not null)
            .ToArray();

        allowedGameCodes.Should().BeEquivalentTo(
            [
                PokerGameMetadataRegistry.FiveCardDrawCode,
                PokerGameMetadataRegistry.OmahaCode
            ]);
    }

    [Fact]
    public async Task Handle_DealersChoice_WithEmptyAllowedGameCodes_ReturnsConflict()
    {
        var command = new CreateGameCommand(
            Guid.NewGuid(),
            string.Empty,
            "DC Table",
            0,
            0,
            new List<PlayerInfo> { new("P1", 1000), new("P2", 1000) },
            IsDealersChoice: true)
        {
            AllowedDealerChoiceGameCodes = []
        };

        var result = await Mediator.Send(command);

        result.IsT1.Should().BeTrue();
        result.AsT1.Reason.Should().Contain("must allow at least one game variant");
    }

    [Fact]
    public async Task Handle_NonDealersChoice_StillWorksNormally()
    {
        // Regression: standard game creation is not broken
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Standard Table",
            10,
            20,
            new List<PlayerInfo> { new("P1", 1000), new("P2", 1000) });

        var result = await Mediator.Send(command);

        result.IsT0.Should().BeTrue();
        var game = await DbContext.Games.FirstAsync(g => g.Id == gameId);
        game.IsDealersChoice.Should().BeFalse();
        game.GameTypeId.Should().NotBeNull();
        game.Ante.Should().Be(10);
        game.MinBet.Should().Be(20);
    }
}
