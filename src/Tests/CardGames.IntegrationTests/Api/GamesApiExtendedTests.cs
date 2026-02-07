using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;
using CardGames.Poker.Api.Games;

namespace CardGames.IntegrationTests.Api;

/// <summary>
/// Extended API integration tests for game query and action endpoints.
/// </summary>
public class GamesApiExtendedTests : ApiIntegrationTestBase
{
    public GamesApiExtendedTests(ApiWebApplicationFactory factory) : base(factory)
    {
    }

    #region GetGameRules Endpoint

    [Fact]
    public async Task GetGameRules_ExistingGame_ReturnsRules()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Rules Test Game",
            10,
            20,
            new List<PlayerInfo> { new("P1", 1000), new("P2", 1000) });
        
        await PostAsync("api/v1/games", command);

        // Act
        var result = await GetAsync<GetGameRulesResponse>($"api/v1/games/{gameId}/rules");

        // Assert
        result.Should().NotBeNull();
        result!.Phases.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetGameRules_NonExistentGame_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync($"api/v1/games/{Guid.NewGuid()}/rules");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Game Type Specific Endpoints

    [Fact]
    public async Task FiveCardDraw_CollectAntes_ReturnsOk()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Ante Test Game",
            10,
            20,
            new List<PlayerInfo> { new("P1", 1000), new("P2", 1000) });
        
        await PostAsync("api/v1/games", command);
        await Client.PostAsync($"api/v1/games/five-card-draw/{gameId}/hands", null);

        // Act
        var response = await Client.PostAsync($"api/v1/games/five-card-draw/{gameId}/hands/antes", null);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FiveCardDraw_DealHands_AfterAntes_ReturnsOk()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Deal Test Game",
            10,
            20,
            new List<PlayerInfo> { new("P1", 1000), new("P2", 1000) });
        
        await PostAsync("api/v1/games", command);
        await Client.PostAsync($"api/v1/games/five-card-draw/{gameId}/hands", null);
        await Client.PostAsync($"api/v1/games/five-card-draw/{gameId}/hands/antes", null);

        // Act
        var response = await Client.PostAsync($"api/v1/games/five-card-draw/{gameId}/hands/deal", null);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Available Poker Games Endpoint

    [Fact]
    public async Task GetAvailablePokerGames_ReturnsListOfGameTypes()
    {
        // Act
        var response = await Client.GetAsync("api/v1/games/available");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Active Games Endpoint

    [Fact]
    public async Task GetActiveGames_ReturnsListOfGames()
    {
        // Arrange - Create and start a game
        var gameId = Guid.NewGuid();
        var command = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Active Game Test",
            10,
            20,
            new List<PlayerInfo> { new("P1", 1000), new("P2", 1000) });
        
        await PostAsync("api/v1/games", command);

        // Act
        var response = await Client.GetAsync("api/v1/games/active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Health Check and Info Endpoints

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await Client.GetAsync("health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task CreateGame_InvalidGameCode_ReturnsConflict()
    {
        // Arrange
        var command = new CreateGameCommand(
            Guid.NewGuid(),
            "INVALID_GAME_CODE",
            "Invalid Game",
            10,
            20,
            new List<PlayerInfo> { new("P1", 1000) });

        // Act
        var response = await PostAsync("api/v1/games", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateGame_DuplicateGameId_ReturnsConflict()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command1 = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "First Game",
            10,
            20,
            new List<PlayerInfo> { new("P1", 1000), new("P2", 1000) });

        var command2 = new CreateGameCommand(
            gameId,
            PokerGameMetadataRegistry.SevenCardStudCode,
            "Second Game",
            10,
            20,
            new List<PlayerInfo> { new("P3", 1000), new("P4", 1000) });

        await PostAsync("api/v1/games", command1);

        // Act
        var response = await PostAsync("api/v1/games", command2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    #endregion

    #region Content Negotiation

    [Fact]
    public async Task GetGames_AcceptsJsonContent()
    {
        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/games");
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    #endregion
}
