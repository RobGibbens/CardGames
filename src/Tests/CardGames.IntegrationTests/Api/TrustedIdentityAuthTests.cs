using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Infrastructure.PipelineBehaviors;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Api;

public class TrustedIdentityAuthTests : IClassFixture<StrictIdentityApiFactory>, IAsyncLifetime
{
	private readonly StrictIdentityApiFactory _factory;
	private HttpClient _client = null!;
	private IServiceScope _scope = null!;

	public TrustedIdentityAuthTests(StrictIdentityApiFactory factory)
	{
		_factory = factory;
	}

	public async Task InitializeAsync()
	{
		_client = _factory.CreateClient();
		_scope = _factory.Services.CreateScope();
		var db = _scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		await db.Database.EnsureCreatedAsync();
	}

	public async Task DisposeAsync()
	{
		var db = _scope.ServiceProvider.GetRequiredService<CardsDbContext>();
		await db.Database.EnsureDeletedAsync();
		_scope.Dispose();
		_client.Dispose();
	}

	[Fact]
	public async Task PostBettingAction_WithoutAuth_Returns401()
	{
		var response = await _client.PostAsJsonAsync(
			$"api/v1/games/hold-em/{Guid.NewGuid()}/betting/actions",
			new { actionType = "Fold", amount = 0 });

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task PostBettingAction_WithForgedEmailHeader_Returns401()
	{
		var request = new HttpRequestMessage(HttpMethod.Post,
			$"api/v1/games/hold-em/{Guid.NewGuid()}/betting/actions")
		{
			Content = JsonContent.Create(new { actionType = "Fold", amount = 0 })
		};
		request.Headers.TryAddWithoutValidation("X-User-Email", "victim@example.com");
		request.Headers.TryAddWithoutValidation("X-User-Authenticated", "true");

		var response = await _client.SendAsync(request);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GetGamePlayers_WithoutAuth_Returns401()
	{
		var response = await _client.GetAsync($"api/v1/games/{Guid.NewGuid()}/players");

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GetCurrentDrawPlayer_WithoutAuth_Returns401()
	{
		var response = await _client.GetAsync($"api/v1/games/{Guid.NewGuid()}/draw-player");

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GetGamePlayers_WithForgedEmailHeader_Returns401()
	{
		var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/games/{Guid.NewGuid()}/players");
		request.Headers.TryAddWithoutValidation("X-User-Email", "victim@example.com");
		request.Headers.TryAddWithoutValidation("X-User-Authenticated", "true");

		var response = await _client.SendAsync(request);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GetCurrentDrawPlayer_WithForgedEmailHeader_Returns401()
	{
		var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/games/{Guid.NewGuid()}/draw-player");
		request.Headers.TryAddWithoutValidation("X-User-Email", "victim@example.com");
		request.Headers.TryAddWithoutValidation("X-User-Authenticated", "true");

		var response = await _client.SendAsync(request);

		response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task PostBettingAction_WithValidInternalToken_IsNotRejectedAsAnonymous()
	{
		var token = InternalUserToken.Issue(
			TrustedIdentityTestKey.Value,
			"player-b",
			"b@example.com",
			"Player B",
			DateTimeOffset.UtcNow.AddMinutes(5));
		var request = new HttpRequestMessage(HttpMethod.Post,
			$"api/v1/games/hold-em/{Guid.NewGuid()}/betting/actions")
		{
			Content = JsonContent.Create(new { actionType = "Fold", amount = 0 })
		};
		request.Headers.TryAddWithoutValidation("X-Internal-User-Token", token);

		var response = await _client.SendAsync(request);

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task DrawCards_ForgedPlayerId_DoesNotAuthorizeTheCaller()
	{
		var token = InternalUserToken.Issue(
			TrustedIdentityTestKey.Value,
			"attacker",
			"attacker@example.com",
			"Attacker",
			DateTimeOffset.UtcNow.AddMinutes(5));
		var request = new HttpRequestMessage(HttpMethod.Post,
			$"api/v1/games/kings-and-lows/{Guid.NewGuid()}/draw")
		{
			Content = JsonContent.Create(new { playerId = Guid.NewGuid(), discardIndices = Array.Empty<int>() })
		};
		request.Headers.TryAddWithoutValidation("X-Internal-User-Token", token);

		var response = await _client.SendAsync(request);

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task JoinGame_WhenCallerIsNotSeated_RejectsWithoutPrivateState()
	{
		var token = InternalUserToken.Issue(
			TrustedIdentityTestKey.Value,
			"attacker",
			"victim@example.com",
			"Victim",
			DateTimeOffset.UtcNow.AddMinutes(5));
		PrivateStateReceived = false;

		await using var connection = new HubConnectionBuilder()
			.WithUrl(new Uri(_factory.Server.BaseAddress, "/hubs/game"), options =>
			{
				options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
				options.AccessTokenProvider = () => Task.FromResult<string?>(token);
			})
			.Build();
		connection.On<JsonElement>("PrivateStateUpdated", _ => PrivateStateReceived = true);

		await connection.StartAsync();
		var act = () => connection.InvokeAsync("JoinGame", Guid.NewGuid());

		await act.Should().ThrowAsync<HubException>().WithMessage("*Not seated*");
		PrivateStateReceived.Should().BeFalse();
	}

	private bool PrivateStateReceived { get; set; }
}

public class GamePlayerResolverTests
{
	[Fact]
	public async Task ResolveSeatedPlayer_MatchesEmailNameOrExternalId_AndSkipsLeftPlayers()
	{
		await using var db = NewDb();
		var gameId = Guid.NewGuid();
		var active = Player(gameId, "a@example.com", "Ada", "ext-a", 0, GamePlayerStatus.Active);
		var left = Player(gameId, "left@example.com", "Left", "ext-left", 1, GamePlayerStatus.Left);
		db.Games.Add(new Game { Id = gameId, CurrentPhase = "PreFlop", CreatedById = "host" });
		db.GamePlayers.AddRange(active, left);
		await db.SaveChangesAsync();

		var byEmail = new GamePlayerResolver(db, new StubUser("other", "someone", "a@example.com", true));
		(await byEmail.ResolveSeatedPlayerAsync(gameId, CancellationToken.None))!.Player.Email.Should().Be("a@example.com");

		var byName = new GamePlayerResolver(db, new StubUser("other", "Ada", null, true));
		(await byName.ResolveSeatedPlayerAsync(gameId, CancellationToken.None))!.SeatPosition.Should().Be(0);

		var byExternal = new GamePlayerResolver(db, new StubUser("ext-a", null, null, true));
		(await byExternal.ResolveSeatedPlayerAsync(gameId, CancellationToken.None))!.Player.ExternalId.Should().Be("ext-a");

		var leftUser = new GamePlayerResolver(db, new StubUser("ext-left", "Left", "left@example.com", true));
		(await leftUser.ResolveSeatedPlayerAsync(gameId, CancellationToken.None)).Should().BeNull();

		(await byEmail.IsTableHostAsync(gameId, CancellationToken.None)).Should().BeFalse();
		var host = new GamePlayerResolver(db, new StubUser("host", "Host", null, true));
		(await host.IsTableHostAsync(gameId, CancellationToken.None)).Should().BeTrue();
	}

	private static CardsDbContext NewDb()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;
		return new CardsDbContext(options);
	}

	private static GamePlayer Player(Guid gameId, string email, string name, string externalId, int seat,
		GamePlayerStatus status) =>
		new()
		{
			GameId = gameId,
			SeatPosition = seat,
			Status = status,
			Player = new Player { Name = name, Email = email, ExternalId = externalId }
		};

	private sealed class StubUser(string? userId, string? userName, string? userEmail, bool isAuthenticated)
		: ICurrentUserService
	{
		public string? UserId => userId;
		public string? UserName => userName;
		public string? UserEmail => userEmail;
		public bool IsAuthenticated => isAuthenticated;
	}
}

public class GameAuthorizationBehaviorTests
{
	[Fact]
	public async Task Betting_WhenAuthenticatedPlayerIsNotCurrentActor_ReturnsNotPlayerTurn()
	{
		await using var db = await SeedBettingAsync(actorSeat: 1);
		var behavior = Behavior(db, "a@example.com", "Ada", "ext-a", http: true);

		var act = () => behavior.Handle(
			new ProcessBettingActionCommand(GameId, BettingActionType.Fold),
			_ => Task.FromResult("should-not-run"),
			CancellationToken.None);

		var error = (await act.Should().ThrowAsync<GameAuthorizationException>()).Which;
		error.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
		error.Code.Should().Be("NotPlayerTurn");
	}

	[Fact]
	public async Task Betting_WhenAuthenticatedPlayerIsCurrentActor_Continues()
	{
		await using var db = await SeedBettingAsync(actorSeat: 0);
		var behavior = Behavior(db, "a@example.com", "Ada", "ext-a", http: true);

		var result = await behavior.Handle(
			new ProcessBettingActionCommand(GameId, BettingActionType.Fold),
			_ => Task.FromResult("ok"),
			CancellationToken.None);

		result.Should().Be("ok");
	}

	[Fact]
	public async Task Betting_WithoutHttpContext_AllowsTimerAutoAction()
	{
		await using var db = await SeedBettingAsync(actorSeat: 1);
		var behavior = Behavior(db, "a@example.com", "Ada", "ext-a", http: false);

		var result = await behavior.Handle(
			new ProcessBettingActionCommand(GameId, BettingActionType.Fold),
			_ => Task.FromResult("timer"),
			CancellationToken.None);

		result.Should().Be("timer");
	}

	private static readonly Guid GameId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

	private static GameAuthorizationBehavior<ProcessBettingActionCommand, string> Behavior(
		CardsDbContext db, string email, string name, string externalId, bool http)
	{
		var accessor = new HttpContextAccessor();
		if (http)
		{
			var identity = new ClaimsIdentity(
			[
				new Claim(ClaimTypes.NameIdentifier, externalId),
				new Claim(ClaimTypes.Email, email),
				new Claim(ClaimTypes.Name, name)
			], "test");
			accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
		}

		var users = new MatchingUser(accessor);
		return new GameAuthorizationBehavior<ProcessBettingActionCommand, string>(
			accessor, users, new GamePlayerResolver(db, users), db);
	}

	private static async Task<CardsDbContext> SeedBettingAsync(int actorSeat)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;
		var db = new CardsDbContext(options);
		db.Games.Add(new Game { Id = GameId, CurrentPhase = "PreFlop", CurrentDrawPlayerIndex = actorSeat });
		db.GamePlayers.Add(new GamePlayer
		{
			GameId = GameId,
			SeatPosition = 0,
			Status = GamePlayerStatus.Active,
			Player = new Player { Name = "Ada", Email = "a@example.com", ExternalId = "ext-a" }
		});
		db.GamePlayers.Add(new GamePlayer
		{
			GameId = GameId,
			SeatPosition = 1,
			Status = GamePlayerStatus.Active,
			Player = new Player { Name = "Bea", Email = "b@example.com", ExternalId = "ext-b" }
		});
		db.BettingRounds.Add(new CardGames.Poker.Api.Data.Entities.BettingRound
		{
			GameId = GameId,
			Street = "PreFlop",
			IsComplete = false,
			CurrentActorIndex = actorSeat,
			RoundNumber = 1
		});
		await db.SaveChangesAsync();
		return db;
	}

	private sealed class MatchingUser(IHttpContextAccessor accessor) : ICurrentUserService
	{
		public string? UserId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
		public string? UserName => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name);
		public string? UserEmail => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);
		public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;
	}
}
