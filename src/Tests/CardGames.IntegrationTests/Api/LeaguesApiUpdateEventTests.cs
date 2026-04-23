using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data.Entities;
using FluentAssertions;
using ContractLeagueOneOffEventType = CardGames.Poker.Api.Contracts.LeagueOneOffEventType;
using DataLeagueRole = CardGames.Poker.Api.Data.Entities.LeagueRole;

namespace CardGames.IntegrationTests.Api;

public class LeaguesApiUpdateEventTests(ApiWebApplicationFactory factory) : ApiIntegrationTestBase(factory)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters =
		{
			new JsonStringEnumConverter()
		}
	};

	[Fact]
	public async Task UpdateSeasonEvent_Admin_Succeeds_AndPersistsChanges()
	{
		SetUser("league-update-season-admin");
		var (leagueId, seasonId, eventId) = await CreateLeagueSeasonAndEventAsync("league-update-season-admin", "Update Season League", "Week 1", 1);
		var scheduledAtUtc = DateTimeOffset.UtcNow.AddDays(14);

		var response = await Client.PutAsJsonAsync($"/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}", new UpdateLeagueSeasonEventRequest
		{
			ScheduledAtUtc = scheduledAtUtc,
			Notes = "Updated schedule details"
		}, JsonOptions);

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var storedEvent = await DbContext.LeagueSeasonEvents.FindAsync(eventId);
		storedEvent.Should().NotBeNull();
		storedEvent!.Name.Should().Be(FormatExpectedSeasonEventName(scheduledAtUtc));
		storedEvent.SequenceNumber.Should().BeNull();
		storedEvent.ScheduledAtUtc.Should().Be(scheduledAtUtc);
		storedEvent.Notes.Should().Be("Updated schedule details");
	}

	[Fact]
	public async Task UpdateSeasonEvent_NonGovernanceMember_IsForbidden()
	{
		SetUser("league-update-season-owner");
		var (leagueId, seasonId, eventId) = await CreateLeagueSeasonAndEventAsync("league-update-season-owner", "Update Season Forbidden League", "Week 1", 1);

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = "league-update-season-member",
			Role = DataLeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetUser("league-update-season-member");
		var response = await Client.PutAsJsonAsync($"/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}", new UpdateLeagueSeasonEventRequest
		{
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(14)
		}, JsonOptions);

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task UpdateSeasonEvent_LaunchedEvent_ReturnsConflict()
	{
		SetUser("league-update-season-launched-admin");
		var (leagueId, seasonId, eventId) = await CreateLeagueSeasonAndEventAsync("league-update-season-launched-admin", "Update Season Conflict League", "Week 1", 1);

		var storedEvent = await DbContext.LeagueSeasonEvents.FindAsync(eventId);
		storedEvent.Should().NotBeNull();
		storedEvent!.LaunchedGameId = Guid.CreateVersion7();
		await DbContext.SaveChangesAsync();

		var response = await Client.PutAsJsonAsync($"/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}", new UpdateLeagueSeasonEventRequest
		{
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(14)
		}, JsonOptions);

		response.StatusCode.Should().Be(HttpStatusCode.Conflict);
	}

	[Fact]
	public async Task UpdateOneOffEvent_Admin_Succeeds_AndPersistsChanges()
	{
		SetUser("league-update-oneoff-admin");
		var (leagueId, eventId) = await CreateOneOffEventAsync("league-update-oneoff-admin", "Update One-Off League", "Friday Night");
		var scheduledAtUtc = DateTimeOffset.UtcNow.AddDays(10);

		var response = await Client.PutAsJsonAsync($"/api/v1/leagues/{leagueId}/events/one-off/{eventId}", new UpdateLeagueOneOffEventRequest
		{
			Name = "Feature Table",
			ScheduledAtUtc = scheduledAtUtc,
			EventType = ContractLeagueOneOffEventType.Tournament,
			Notes = "Bring chips",
			GameTypeCode = "OMAHA",
			Ante = 25,
			MinBet = 50
		}, JsonOptions);

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var storedEvent = await DbContext.LeagueOneOffEvents.FindAsync(eventId);
		storedEvent.Should().NotBeNull();
		storedEvent!.Name.Should().Be("Feature Table");
		storedEvent.ScheduledAtUtc.Should().Be(scheduledAtUtc);
		storedEvent.EventType.Should().Be(CardGames.Poker.Api.Data.Entities.LeagueOneOffEventType.Tournament);
		storedEvent.GameTypeCode.Should().Be("OMAHA");
		storedEvent.Ante.Should().Be(25);
		storedEvent.MinBet.Should().Be(50);
		storedEvent.Notes.Should().Be("Bring chips");
	}

	[Fact]
	public async Task UpdateOneOffEvent_NonGovernanceMember_IsForbidden()
	{
		SetUser("league-update-oneoff-owner");
		var (leagueId, eventId) = await CreateOneOffEventAsync("league-update-oneoff-owner", "Update One-Off Forbidden League", "Friday Night");

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = "league-update-oneoff-member",
			Role = DataLeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetUser("league-update-oneoff-member");
		var response = await Client.PutAsJsonAsync($"/api/v1/leagues/{leagueId}/events/one-off/{eventId}", new UpdateLeagueOneOffEventRequest
		{
			Name = "Friday Night Updated",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(12),
			EventType = ContractLeagueOneOffEventType.CashGame,
			GameTypeCode = "HOLDEM"
		}, JsonOptions);

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task UpdateOneOffEvent_LaunchedEvent_ReturnsConflict()
	{
		SetUser("league-update-oneoff-launched-admin");
		var (leagueId, eventId) = await CreateOneOffEventAsync("league-update-oneoff-launched-admin", "Update One-Off Conflict League", "Friday Night");

		var storedEvent = await DbContext.LeagueOneOffEvents.FindAsync(eventId);
		storedEvent.Should().NotBeNull();
		storedEvent!.LaunchedGameId = Guid.CreateVersion7();
		await DbContext.SaveChangesAsync();

		var response = await Client.PutAsJsonAsync($"/api/v1/leagues/{leagueId}/events/one-off/{eventId}", new UpdateLeagueOneOffEventRequest
		{
			Name = "Friday Night Updated",
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(12),
			EventType = ContractLeagueOneOffEventType.CashGame,
			GameTypeCode = "HOLDEM"
		}, JsonOptions);

		response.StatusCode.Should().Be(HttpStatusCode.Conflict);
	}

	private async Task<(Guid LeagueId, Guid SeasonId, Guid EventId)> CreateLeagueSeasonAndEventAsync(string ownerUserId, string leagueName, string eventName, int? sequenceNumber)
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
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3)
		});
		createEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var seasonEvent = await createEventResponse.Content.ReadFromJsonAsync<CreateLeagueSeasonEventResponse>(JsonOptions);
		seasonEvent.Should().NotBeNull();

		return (league.LeagueId, season.SeasonId, seasonEvent!.EventId);
	}

	private async Task<(Guid LeagueId, Guid EventId)> CreateOneOffEventAsync(string ownerUserId, string leagueName, string eventName)
	{
		SetUser(ownerUserId);

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = leagueName
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createEventResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/events/one-off", new CreateLeagueOneOffEventRequest
		{
			Name = eventName,
			ScheduledAtUtc = DateTimeOffset.UtcNow.AddDays(3),
			EventType = ContractLeagueOneOffEventType.CashGame,
			GameTypeCode = "HOLDEM",
			Ante = 10,
			MinBet = 20
		});
		createEventResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var oneOffEvent = await createEventResponse.Content.ReadFromJsonAsync<CreateLeagueOneOffEventResponse>(JsonOptions);
		oneOffEvent.Should().NotBeNull();

		return (league.LeagueId, oneOffEvent!.EventId);
	}

	private void SetUser(string userId)
	{
		Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserHeader);
		Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
	}

	private static string FormatExpectedSeasonEventName(DateTimeOffset scheduledAtUtc)
	{
		return scheduledAtUtc.ToUniversalTime().ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture);
	}
}