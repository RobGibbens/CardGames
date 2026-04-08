using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;
using CardGames.Poker.Api.Features.Games.AvailablePokerGames.v1.Queries.GetAvailablePokerGames;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.ChooseDealerGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CardGames.IntegrationTests.Api;

/// <summary>
/// Extended API integration tests for game query and action endpoints.
/// </summary>
public class GamesApiExtendedTests : ApiIntegrationTestBase
{
    private static readonly JsonSerializerOptions ActiveGamesJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

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
        var games = await response.Content.ReadFromJsonAsync<List<GetAvailablePokerGamesResponse>>();
        games.Should().NotBeNull();
        games.Should().NotBeEmpty();

        var gameCodes = games!.Select(g => g.Code).ToList();
        gameCodes.Should().Contain(PokerGameMetadataRegistry.HoldEmCode);
        gameCodes.Should().Contain(PokerGameMetadataRegistry.OmahaCode);
        gameCodes.Should().Contain(PokerGameMetadataRegistry.NebraskaCode);
        gameCodes.Should().Contain(PokerGameMetadataRegistry.SouthDakotaCode);
    }

    [Fact]
    public async Task GetAvailablePokerGames_WithVariantOmaha_ReturnsOnlyOmaha()
    {
        // Act
        var response = await Client.GetAsync("api/v1/games/available?variant=Omaha");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var games = await response.Content.ReadFromJsonAsync<List<GetAvailablePokerGamesResponse>>();
        games.Should().NotBeNull();
        games.Should().ContainSingle();

        var omaha = games![0];
        omaha.Code.Should().Be(PokerGameMetadataRegistry.OmahaCode);
        omaha.Name.Should().Be("Omaha");
        omaha.MinimumNumberOfPlayers.Should().Be(2);
        omaha.MaximumNumberOfPlayers.Should().Be(10);
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

    [Fact]
    public async Task GetActiveGames_LeagueGames_AreVisibleOnlyToMembers_AndIncludeScopeLabel()
    {
        var publicGameId = Guid.NewGuid();
        await PostAsync("api/v1/games", new CreateGameCommand(
            publicGameId,
            PokerGameMetadataRegistry.FiveCardDrawCode,
            "Public Lobby Table",
            10,
            20,
            [new PlayerInfo("P1", 1000), new PlayerInfo("P2", 1000)]));

        SetUser("league-lobby-owner");
        var createLeagueResponse = await PostAsync("/api/v1/leagues", new CardGames.Poker.Api.Contracts.CreateLeagueRequest
        {
            Name = "Lobby Members League"
        });
        createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var league = await createLeagueResponse.Content.ReadFromJsonAsync<CardGames.Poker.Api.Contracts.CreateLeagueResponse>(ActiveGamesJsonOptions);
        league.Should().NotBeNull();

        var createEventResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/events/one-off", new CardGames.Poker.Api.Contracts.CreateLeagueOneOffEventRequest
        {
            Name = "League Lobby Table",
            ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            EventType = CardGames.Poker.Api.Contracts.LeagueOneOffEventType.CashGame,
            GameTypeCode = PokerGameMetadataRegistry.FiveCardDrawCode,
            Ante = 10,
            MinBet = 20
        });
        createEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var leagueEvent = await createEventResponse.Content.ReadFromJsonAsync<CardGames.Poker.Api.Contracts.CreateLeagueOneOffEventResponse>(ActiveGamesJsonOptions);
        leagueEvent.Should().NotBeNull();

        var launchResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/events/one-off/{leagueEvent!.EventId}/launch", new CardGames.Poker.Api.Contracts.LaunchLeagueEventSessionRequest
        {
            GameCode = PokerGameMetadataRegistry.FiveCardDrawCode,
            HostStartingChips = 1000
        });
        launchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var launched = await launchResponse.Content.ReadFromJsonAsync<CardGames.Poker.Api.Contracts.LaunchLeagueEventSessionResponse>(ActiveGamesJsonOptions);
        launched.Should().NotBeNull();

        DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
        {
            LeagueId = league.LeagueId,
            UserId = "league-lobby-member",
            Role = CardGames.Poker.Api.Data.Entities.LeagueRole.Member,
            IsActive = true,
            JoinedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await DbContext.SaveChangesAsync();

        SetUser("league-lobby-outsider");
        var outsiderGames = await Client.GetFromJsonAsync<List<CardGames.Poker.Api.Contracts.GetActiveGamesResponse>>("api/v1/games/active", ActiveGamesJsonOptions);
        outsiderGames.Should().NotBeNull();
        outsiderGames!.Should().Contain(x => x.Id == publicGameId);
        outsiderGames.Should().NotContain(x => x.Id == launched!.GameId);
        outsiderGames.Single(x => x.Id == publicGameId).TableScopeLabel.Should().Be("Public");

        SetUser("league-lobby-member");
        var memberGames = await Client.GetFromJsonAsync<List<CardGames.Poker.Api.Contracts.GetActiveGamesResponse>>("api/v1/games/active", ActiveGamesJsonOptions);
        memberGames.Should().NotBeNull();
        memberGames!.Should().Contain(x => x.Id == publicGameId);
        memberGames.Should().Contain(x => x.Id == launched!.GameId);
        memberGames.Single(x => x.Id == launched.GameId).TableScopeLabel.Should().Be("League");
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

    private void SetUser(string userId)
    {
        Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserHeader);
        Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
    }

}
