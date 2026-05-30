using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Infrastructure;
using CardGames.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ApiBettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;
using PokerBettingStructure = CardGames.Poker.Betting.BettingStructure;

namespace CardGames.IntegrationTests.Api;

public sealed class GameEndpointAuthorizationTests : IAsyncLifetime
{
    private const string Issuer = "CardGames.Internal";
    private const string Audience = "CardGames.Poker.Api";
    private const string SigningKey = "dev-only-cardgames-internal-api-signing-key-20260529";

    private static readonly TestIdentity HostIdentity = new("host-user", "host@example.com");
    private static readonly TestIdentity GuestIdentity = new("guest-user", "guest@example.com");
    private static readonly TestIdentity OutsiderIdentity = new("outsider-user", "outsider@example.com");

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
    public async Task Anonymous_Callers_Cannot_Invoke_Protected_Game_Endpoints()
    {
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var joinRequestId = Guid.NewGuid();

        using var addChipsRequest = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/games/{gameId}/players/{playerId}/add-chips",
            body: new { amount = 100 });

        using var bettingRequest = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/games/hold-em/{gameId}/betting/actions",
            body: new { actionType = ApiBettingActionType.Check, amount = 0 });

        using var resolveJoinRequest = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/games/{gameId}/join-requests/{joinRequestId}/resolve",
            body: new { approved = true, approvedBuyIn = 1000, denialReason = (string?)null });

        using var updateSettingsRequest = CreateRequest(
            HttpMethod.Put,
            $"/api/v1/games/{gameId}/settings",
            body: new { name = "Renamed Table", ante = 5, minBet = 10, smallBlind = 5, bigBlind = 10, rowVersion = "AQ==" });

        var addChipsResponse = await Client.SendAsync(addChipsRequest);
        var bettingResponse = await Client.SendAsync(bettingRequest);
        var resolveJoinResponse = await Client.SendAsync(resolveJoinRequest);
        var updateSettingsResponse = await Client.SendAsync(updateSettingsRequest);

        addChipsResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        bettingResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resolveJoinResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        updateSettingsResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Public_Game_Endpoints_Remain_Public()
    {
        var seededGame = await SeedHoldEmGameAsync();

        var getGamesResponse = await Client.GetAsync("/api/v1/games");
        var getActiveGamesResponse = await Client.GetAsync("/api/v1/games/active");
        var getAvailableGamesResponse = await Client.GetAsync("/api/v1/games/available");
        var getGameResponse = await Client.GetAsync($"/api/v1/games/{seededGame.GameId}");
        var getRulesResponse = await Client.GetAsync($"/api/v1/games/{seededGame.GameId}/rules");

        getGamesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getActiveGamesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getAvailableGamesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getGameResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        getRulesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Authenticated_NonHosts_Are_Forbidden_From_Host_Only_Operations()
    {
        var seededGame = await SeedHoldEmGameAsync(includePendingJoinRequest: true);

        using var startHandRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/games/hold-em/{seededGame.GameId}/start",
            GuestIdentity);

        using var updateSettingsRequest = CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/games/{seededGame.GameId}/settings",
            GuestIdentity,
            new { name = "Guest Rename", ante = 5, minBet = 10, smallBlind = 5, bigBlind = 10, rowVersion = "AQ==" });

        using var resolveJoinRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/games/{seededGame.GameId}/join-requests/{seededGame.PendingJoinRequestId}/resolve",
            GuestIdentity,
            new { approved = true, approvedBuyIn = 1000, denialReason = (string?)null });

        var startHandResponse = await Client.SendAsync(startHandRequest);
        var updateSettingsResponse = await Client.SendAsync(updateSettingsRequest);
        var resolveJoinResponse = await Client.SendAsync(resolveJoinRequest);

        startHandResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        updateSettingsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        resolveJoinResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Authenticated_NonParticipants_Are_Forbidden_From_Private_Game_State_And_Actions()
    {
        var seededGame = await SeedHoldEmGameAsync();

        using var getPlayersRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/games/{seededGame.GameId}/players",
            OutsiderIdentity);

        using var bettingRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/games/hold-em/{seededGame.GameId}/betting/actions",
            OutsiderIdentity,
            new { actionType = ApiBettingActionType.Check, amount = 0 });

        var getPlayersResponse = await Client.SendAsync(getPlayersRequest);
        var bettingResponse = await Client.SendAsync(bettingRequest);

        getPlayersResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        bettingResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Caller_Can_Only_Add_Chips_For_Their_Own_Player_Record()
    {
        var seededGame = await SeedHoldEmGameAsync();

        using var otherPlayerRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/games/{seededGame.GameId}/players/{seededGame.GuestPlayerId}/add-chips",
            HostIdentity,
            new { amount = 100 });

        using var ownPlayerRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/games/{seededGame.GameId}/players/{seededGame.HostPlayerId}/add-chips",
            HostIdentity,
            new { amount = 100 });

        var otherPlayerResponse = await Client.SendAsync(otherPlayerRequest);
        var ownPlayerResponse = await Client.SendAsync(ownPlayerRequest);

        otherPlayerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        ownPlayerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Host_Can_Start_A_Game_And_Participants_Can_Read_Private_Player_State()
    {
        var seededGame = await SeedHoldEmGameAsync();

        using var getPlayersRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/games/{seededGame.GameId}/players",
            GuestIdentity);

        using var startHandRequest = CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/games/hold-em/{seededGame.GameId}/start",
            HostIdentity);

        var getPlayersResponse = await Client.SendAsync(getPlayersRequest);
        var startHandResponse = await Client.SendAsync(startHandRequest);

        getPlayersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        startHandResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<SeededGame> SeedHoldEmGameAsync(bool includePendingJoinRequest = false)
    {
        var now = DateTimeOffset.UtcNow;
        var gameType = new GameType
        {
            Name = "Texas Hold'em",
            Code = PokerGameMetadataRegistry.HoldEmCode,
            BettingStructure = PokerBettingStructure.Blinds,
            MinPlayers = 2,
            MaxPlayers = 10,
            InitialHoleCards = 2,
            InitialBoardCards = 0,
            MaxCommunityCards = 5,
            MaxPlayerCards = 2,
            HasDrawPhase = false,
            MaxDiscards = 0,
            WildCardRule = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        var hostPlayer = CreatePlayer(HostIdentity, now);
        var guestPlayer = CreatePlayer(GuestIdentity, now);

        var game = new Game
        {
            Id = Guid.CreateVersion7(),
            GameTypeId = gameType.Id,
            GameType = gameType,
            Name = "Authorization Test Table",
            CurrentPhase = "WaitingToStart",
            DealerPosition = 0,
            SmallBlind = 5,
            BigBlind = 10,
            MinBet = 10,
            Status = GameStatus.WaitingForPlayers,
            CurrentPlayerIndex = 0,
            CurrentDrawPlayerIndex = -1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedById = HostIdentity.UserId,
            CreatedByName = HostIdentity.Email,
            UpdatedById = HostIdentity.UserId,
            UpdatedByName = HostIdentity.Email
        };

        game.GamePlayers.Add(CreateGamePlayer(game, hostPlayer, seatPosition: 0, now));
        game.GamePlayers.Add(CreateGamePlayer(game, guestPlayer, seatPosition: 1, now));

        GameJoinRequest? pendingJoinRequest = null;
        if (includePendingJoinRequest)
        {
            var requester = CreatePlayer(new TestIdentity("requester-user", "requester@example.com"), now);
            pendingJoinRequest = new GameJoinRequest
            {
                Id = Guid.CreateVersion7(),
                GameId = game.Id,
                Game = game,
                PlayerId = requester.Id,
                Player = requester,
                RequestedBuyIn = 1000,
                SeatIndex = 2,
                Status = GameJoinRequestStatus.Pending,
                RequestedAt = now,
                UpdatedAt = now,
                ExpiresAt = now.AddHours(1)
            };

            game.JoinRequests.Add(pendingJoinRequest);
            _dbContext.Players.Add(requester);
        }

        _dbContext.GameTypes.Add(gameType);
        _dbContext.Players.AddRange(hostPlayer, guestPlayer);
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        return new SeededGame(game.Id, hostPlayer.Id, guestPlayer.Id, pendingJoinRequest?.Id);
    }

    private static Player CreatePlayer(TestIdentity identity, DateTimeOffset now) => new()
    {
        Name = identity.Email,
        Email = identity.Email,
        ExternalId = identity.UserId,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static GamePlayer CreateGamePlayer(Game game, Player player, int seatPosition, DateTimeOffset now) => new()
    {
        Game = game,
        GameId = game.Id,
        Player = player,
        PlayerId = player.Id,
        SeatPosition = seatPosition,
        ChipStack = 1000,
        StartingChips = 1000,
        Status = GamePlayerStatus.Active,
        JoinedAt = now
    };

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string url,
        TestIdentity identity,
        object? body = null) => CreateRequest(method, url, identity, body);

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string url,
        TestIdentity? identity = null,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);

        if (identity is not null)
        {
            request.Headers.TryAddWithoutValidation(
                InternalApiAuthenticationHandler.InternalTokenHeaderName,
                CreateInternalToken(identity.UserId, identity.Email, identity.Email));
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static string CreateInternalToken(string userId, string userName, string email)
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

    private sealed record TestIdentity(string UserId, string Email);

    private sealed record SeededGame(Guid GameId, Guid HostPlayerId, Guid GuestPlayerId, Guid? PendingJoinRequestId);
}