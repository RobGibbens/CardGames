using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace CardGames.IntegrationTests.Api;

public sealed class ApiAuthenticationBoundaryTests : IAsyncLifetime
{
    private const string Issuer = "CardGames.Internal";
    private const string Audience = "CardGames.Poker.Api";
    private const string SigningKey = "dev-only-cardgames-internal-api-signing-key-20260529";

    private readonly RealAuthApiWebApplicationFactory _factory = new();
    private IServiceScope _scope = null!;
    private CardsDbContext _dbContext = null!;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Client = _factory.CreateClient();
        _scope = _factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<CardsDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();

        if (_dbContext is not null)
        {
            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.DisposeAsync();
        }

        _scope.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Forged_Identity_Headers_Do_Not_Authenticate_Protected_Endpoint()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/leagues/mine");
        request.Headers.TryAddWithoutValidation("X-User-Id", "attacker-user");
        request.Headers.TryAddWithoutValidation("X-User-Name", "attacker@example.com");
        request.Headers.TryAddWithoutValidation("X-User-Email", "attacker@example.com");
        request.Headers.TryAddWithoutValidation("X-User-Authenticated", "true");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forged_Query_String_Identity_Does_Not_Authenticate_Protected_Endpoint()
    {
        var response = await Client.GetAsync("/api/v1/leagues/mine?userId=attacker-user&authenticated=true&userName=attacker@example.com");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Signed_Internal_Token_Allows_Protected_Endpoint()
    {
        using var request = CreateInternalRequest(HttpMethod.Get, "/api/v1/leagues/mine");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Internal_Token_Transport_Does_Not_Expand_To_Normal_Api_Bearer_Or_Query_Flows()
    {
        using var bearerRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/leagues/mine");
        bearerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateInternalToken());

        var bearerResponse = await Client.SendAsync(bearerRequest);
        bearerResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var queryResponse = await Client.GetAsync($"/api/v1/leagues/mine?access_token={Uri.EscapeDataString(CreateInternalToken())}");
        queryResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Public_Game_Query_Remains_Public_And_Game_Create_Requires_Authentication()
    {
        var getGamesResponse = await Client.GetAsync("/api/v1/games");
        getGamesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createCommand = new CreateGameCommand(
            Guid.NewGuid(),
            PokerGameMetadataRegistry.HoldEmCode,
            "Secured Game",
            5,
            10,
            [new PlayerInfo("Player1", 1000), new PlayerInfo("Player2", 1000)]);

        var anonymousCreateResponse = await Client.PostAsJsonAsync("/api/v1/games", createCommand);
        anonymousCreateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var authenticatedRequest = CreateInternalRequest(HttpMethod.Post, "/api/v1/games", createCommand);
        var authenticatedCreateResponse = await Client.SendAsync(authenticatedRequest);

        authenticatedCreateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task League_Membership_History_Allows_Active_Members_And_Rejects_Authenticated_Outsiders()
    {
        using var createLeagueRequest = CreateInternalRequest(
            HttpMethod.Post,
            "/api/v1/leagues",
            new CardGames.Poker.Api.Contracts.CreateLeagueRequest { Name = "Security Regression League" },
            token: CreateInternalToken(userId: "league-owner", userName: "league-owner@example.com", email: "league-owner@example.com"));

        var createLeagueResponse = await Client.SendAsync(createLeagueRequest);
        createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var leagueDocument = await JsonDocument.ParseAsync(await createLeagueResponse.Content.ReadAsStreamAsync());
        var leagueId = leagueDocument.RootElement.GetProperty("leagueId").GetGuid();

        using var ownerHistoryRequest = CreateInternalRequest(
            HttpMethod.Get,
            $"/api/v1/leagues/{leagueId}/members/history",
            token: CreateInternalToken(userId: "league-owner", userName: "league-owner@example.com", email: "league-owner@example.com"));

        using var outsiderHistoryRequest = CreateInternalRequest(
            HttpMethod.Get,
            $"/api/v1/leagues/{leagueId}/members/history",
            token: CreateInternalToken(userId: "league-outsider", userName: "league-outsider@example.com", email: "league-outsider@example.com"));

        var ownerHistoryResponse = await Client.SendAsync(ownerHistoryRequest);
        var outsiderHistoryResponse = await Client.SendAsync(outsiderHistoryRequest);

        ownerHistoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        outsiderHistoryResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LobbyHub_Negotiate_Rejects_Forged_Query_Identity_And_Accepts_Internal_Token()
    {
        var forgedResponse = await Client.PostAsync(
            "/hubs/lobby/negotiate?negotiateVersion=1&userId=attacker-user&authenticated=true",
            content: null);

        forgedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var negotiateRequest = new HttpRequestMessage(HttpMethod.Post, "/hubs/lobby/negotiate?negotiateVersion=1");
        negotiateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateInternalToken());

        var authenticatedResponse = await Client.SendAsync(negotiateRequest);

        authenticatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static HttpRequestMessage CreateInternalRequest(HttpMethod method, string url, object? jsonBody = null, string? token = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation(
            InternalApiAuthenticationHandler.InternalTokenHeaderName,
            token ?? CreateInternalToken());

        if (jsonBody is not null)
        {
            request.Content = JsonContent.Create(jsonBody);
        }

        return request;
    }

    private static string CreateInternalToken(
        string userId = "test-user",
        string userName = "test-user@example.com",
        string email = "test-user@example.com")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
            new(ClaimTypes.Name, userName),
            new("preferred_username", userName),
            new(ClaimTypes.Email, email),
            new("email", email)
        };

        var now = DateTime.UtcNow;
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(5),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}