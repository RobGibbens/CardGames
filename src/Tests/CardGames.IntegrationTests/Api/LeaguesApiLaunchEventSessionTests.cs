using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data.Entities;
using FluentAssertions;

namespace CardGames.IntegrationTests.Api;

public class LeaguesApiLaunchEventSessionTests(ApiWebApplicationFactory factory) : ApiIntegrationTestBase(factory)
{
	private const int SeasonTournamentBuyIn = 300;

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters =
		{
			new JsonStringEnumConverter()
		}
	};

	[Fact]
	[Trait("QualityGate", "LeaguesP0")]
	public async Task LaunchSeasonEventSession_Admin_Succeeds_AndPersistsLinkage()
	{
		SetUser("league-launch-admin");
		var (leagueId, seasonId, eventId) = await CreateLeagueSeasonAndEventAsync("league-launch-admin", "Admin Launch League");

		var launchResponse = await PostAsync($"/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}/launch", new LaunchLeagueEventSessionRequest
		{
			GameCode = "FIVECARDDRAW",
			GameName = "Week 1 Table",
			Ante = 10,
			MinBet = 20,
			HostStartingChips = 300
		});

		launchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var launched = await launchResponse.Content.ReadFromJsonAsync<LaunchLeagueEventSessionResponse>(JsonOptions);
		launched.Should().NotBeNull();
		launched!.LeagueId.Should().Be(leagueId);
		launched.EventId.Should().Be(eventId);
		launched.GameId.Should().NotBe(Guid.Empty);
		launched.TablePath.Should().Be($"/table/{launched.GameId}");

		var storedEvent = await DbContext.LeagueSeasonEvents.FindAsync(eventId);
		storedEvent.Should().NotBeNull();
		storedEvent!.LaunchedGameId.Should().Be(launched.GameId);
		storedEvent.LaunchedByUserId.Should().Be("league-launch-admin");
		storedEvent.LaunchedAtUtc.Should().NotBeNull();

		var storedGame = await DbContext.Games.FindAsync(launched.GameId);
		storedGame.Should().NotBeNull();
		storedGame!.TournamentBuyIn.Should().Be(SeasonTournamentBuyIn);

		var hostGamePlayer = await DbContext.GamePlayers.SingleAsync(x => x.GameId == launched.GameId && x.SeatPosition == 0);
		hostGamePlayer.StartingChips.Should().Be(SeasonTournamentBuyIn);
	}

	[Fact]
	public async Task LaunchSeasonEventSession_Manager_Succeeds()
	{
		SetUser("league-launch-owner");
		var (leagueId, seasonId, eventId) = await CreateLeagueSeasonAndEventAsync("league-launch-owner", "Manager Launch League");

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = "league-launch-manager",
			Role = CardGames.Poker.Api.Data.Entities.LeagueRole.Manager,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetUser("league-launch-manager");
		var launchResponse = await PostAsync($"/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}/launch", new LaunchLeagueEventSessionRequest
		{
			GameCode = "FIVECARDDRAW",
			HostStartingChips = 250
		});

		launchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task LaunchSeasonEventSession_NonGovernanceMember_IsForbidden()
	{
		SetUser("league-launch-owner2");
		var (leagueId, seasonId, eventId) = await CreateLeagueSeasonAndEventAsync("league-launch-owner2", "Forbidden Launch League");

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = "league-launch-member",
			Role = CardGames.Poker.Api.Data.Entities.LeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetUser("league-launch-member");
		var launchResponse = await PostAsync($"/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}/launch", new LaunchLeagueEventSessionRequest
		{
			GameCode = "FIVECARDDRAW",
			HostStartingChips = 200
		});

		launchResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task LaunchSeasonEventSession_InvalidOrMismatchedEvent_Fails()
	{
		SetUser("league-launch-owner3");
		var (leagueAId, seasonAId, eventAId) = await CreateLeagueSeasonAndEventAsync("league-launch-owner3", "Mismatch League A");
		var (leagueBId, seasonBId, _) = await CreateLeagueSeasonAndEventAsync("league-launch-owner3", "Mismatch League B");

		var invalidEventResponse = await PostAsync($"/api/v1/leagues/{leagueAId}/seasons/{seasonAId}/events/{Guid.NewGuid()}/launch", new LaunchLeagueEventSessionRequest
		{
			GameCode = "FIVECARDDRAW",
			HostStartingChips = 200
		});
		invalidEventResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

		var mismatchedLeagueResponse = await PostAsync($"/api/v1/leagues/{leagueBId}/seasons/{seasonBId}/events/{eventAId}/launch", new LaunchLeagueEventSessionRequest
		{
			GameCode = "FIVECARDDRAW",
			HostStartingChips = 200
		});
		mismatchedLeagueResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	[Trait("QualityGate", "LeaguesP0")]
	public async Task JoinGame_ForLeagueLaunchedSession_RequiresActiveLeagueMembership()
	{
		SetUser("league-join-owner");
		var (leagueId, seasonId, eventId) = await CreateLeagueSeasonAndEventAsync("league-join-owner", "Join Gate League");

		var launchResponse = await PostAsync($"/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}/launch", new LaunchLeagueEventSessionRequest
		{
			GameCode = "FIVECARDDRAW",
			HostStartingChips = 300
		});
		launchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var launched = await launchResponse.Content.ReadFromJsonAsync<LaunchLeagueEventSessionResponse>(JsonOptions);
		launched.Should().NotBeNull();

		await SeedWalletForUserAsync("league-active-member", 1_000);
		await SeedWalletForUserAsync("league-outsider", 1_000);

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = "league-active-member",
			Role = CardGames.Poker.Api.Data.Entities.LeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetUser("league-outsider");
		var outsiderJoin = await PostAsync($"/api/v1/games/{launched!.GameId}/players", new JoinGameRequest(1, 100));
		outsiderJoin.StatusCode.Should().Be(HttpStatusCode.Forbidden);

		SetUser("league-active-member");
		var memberJoin = await PostAsync($"/api/v1/games/{launched.GameId}/players", new JoinGameRequest(1, SeasonTournamentBuyIn));
		memberJoin.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	[Trait("QualityGate", "LeaguesP0")]
	public async Task LaunchSeasonEventSession_SameEventTwice_ReturnsConflict_AndPreservesOriginalSessionLinkage()
	{
		SetUser("league-launch-twice-admin");
		var (leagueId, seasonId, eventId) = await CreateLeagueSeasonAndEventAsync("league-launch-twice-admin", "Launch Twice League");

		var firstLaunchResponse = await PostAsync($"/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}/launch", new LaunchLeagueEventSessionRequest
		{
			GameCode = "FIVECARDDRAW",
			HostStartingChips = 250
		});
		firstLaunchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var firstLaunch = await firstLaunchResponse.Content.ReadFromJsonAsync<LaunchLeagueEventSessionResponse>(JsonOptions);
		firstLaunch.Should().NotBeNull();

		var secondLaunchResponse = await PostAsync($"/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}/launch", new LaunchLeagueEventSessionRequest
		{
			GameCode = "FIVECARDDRAW",
			HostStartingChips = 500
		});
		secondLaunchResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

		var storedEvent = await DbContext.LeagueSeasonEvents.FindAsync(eventId);
		storedEvent.Should().NotBeNull();
		storedEvent!.LaunchedGameId.Should().Be(firstLaunch!.GameId);
	}

	private async Task<(Guid LeagueId, Guid SeasonId, Guid EventId)> CreateLeagueSeasonAndEventAsync(string ownerUserId, string leagueName)
	{
		SetUser(ownerUserId);

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = leagueName
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createSeasonResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/seasons", new CreateLeagueSeasonRequest
		{
			Name = "Spring 2026"
		});
		createSeasonResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var season = await createSeasonResponse.Content.ReadFromJsonAsync<CreateLeagueSeasonResponse>(JsonOptions);
		season.Should().NotBeNull();

		var createEventResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons/{season!.SeasonId}/events", new CreateLeagueSeasonEventRequest
		{
			Name = "Week 1",
			SequenceNumber = 1,
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3),
			TournamentBuyIn = SeasonTournamentBuyIn
		});
		createEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var seasonEvent = await createEventResponse.Content.ReadFromJsonAsync<CreateLeagueSeasonEventResponse>(JsonOptions);
		seasonEvent.Should().NotBeNull();

		return (league.LeagueId, season.SeasonId, seasonEvent!.EventId);
	}

	private async Task SeedWalletForUserAsync(string userName, int balance)
	{
		var player = DbContext.Players.FirstOrDefault(x => x.Name == userName);
		if (player is null)
		{
			player = new Player
			{
				Id = Guid.CreateVersion7(),
				Name = userName,
				Email = userName,
				IsActive = true,
				TotalGamesPlayed = 0,
				TotalHandsPlayed = 0,
				TotalHandsWon = 0,
				TotalChipsWon = 0,
				TotalChipsLost = 0,
				CreatedAt = DateTimeOffset.UtcNow,
				UpdatedAt = DateTimeOffset.UtcNow
			};
			DbContext.Players.Add(player);
		}

		var account = DbContext.PlayerChipAccounts.FirstOrDefault(x => x.PlayerId == player.Id);
		if (account is null)
		{
			account = new PlayerChipAccount
			{
				PlayerId = player.Id,
				Balance = balance,
				CreatedAtUtc = DateTimeOffset.UtcNow,
				UpdatedAtUtc = DateTimeOffset.UtcNow
			};
			DbContext.PlayerChipAccounts.Add(account);
		}
		else
		{
			account.Balance = balance;
			account.UpdatedAtUtc = DateTimeOffset.UtcNow;
		}

		await DbContext.SaveChangesAsync();
	}

	private void SetUser(string userId)
	{
		Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserHeader);
		Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
	}
}
