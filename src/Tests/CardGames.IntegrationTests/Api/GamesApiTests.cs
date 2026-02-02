using System.Net;
using System.Net.Http.Json;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGames;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.StartHand;
using CardGames.Poker.Api.Games;
using FluentAssertions;

namespace CardGames.IntegrationTests.Api;

public class GamesApiTests : ApiIntegrationTestBase
{
    public GamesApiTests(ApiWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateGame_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var command = new CreateGameCommand(
            Guid.NewGuid(),
            PokerGameMetadataRegistry.HoldEmCode,
            "Test Game",
            10,
            20,
            new List<PlayerInfo>
            {
                new("Player1", 1000),
                new("Player2", 1000)
            });

        // Act
        var response = await PostAsync("api/v1/games", command);
        var xxx = await response.Content.ReadAsStringAsync();
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdId = await response.Content.ReadFromJsonAsync<Guid>();
        createdId.Should().Be(command.GameId);

        // Verify it exists in DB
        var game = await DbContext.Games.FindAsync(command.GameId);
        game.Should().NotBeNull();
    }

    [Fact]
    public async Task GetGames_AfterCreation_ReturnsList()
    {
        // Arrange
        var command = new CreateGameCommand(
            Guid.NewGuid(),
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "List Game",
            10,
            20,
            new List<PlayerInfo> { new("P1", 1000) });
        
        await PostAsync("api/v1/games", command);

        // Act
        var xxx = await Client.GetAsync("api/v1/games");
        var dfdf = await xxx.Content.ReadAsStringAsync();
        var result = await GetAsync<List<GetGamesResponse>>("api/v1/games");

        // Assert
        result.Should().NotBeNull();
        result!.Should().Contain(g => g.Id == command.GameId);
    }

    [Fact]
    public async Task StartHand_KingsAndLows_ReturnsOk()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            "KINGSANDLOWS", 
            "Kings And Lows Game",
            10,
            20,
            new List<PlayerInfo>
            {
                new("Player1", 1000),
                new("Player2", 1000)
            });
        
        await PostAsync("api/v1/games", command);

        // Act
        var response = await Client.PostAsync($"api/v1/games/kings-and-lows/{gameId}/start-hand", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Check if hand started by querying game status
        var game = await DbContext.Games.FindAsync(gameId);
        game.Should().NotBeNull();
        // Kings And Lows starts with Dealing? Or WaitingForPlayers -> Playing
        game!.Status.Should().Be(GameStatus.InProgress);
    }
}
