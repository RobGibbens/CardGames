using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data.Entities;
using FluentAssertions;
using ContractLeagueOneOffEventType = CardGames.Poker.Api.Contracts.LeagueOneOffEventType;
using DataLeagueRole = CardGames.Poker.Api.Data.Entities.LeagueRole;

namespace CardGames.IntegrationTests.Api;

public class LeaguesApiDeleteEventTests(ApiWebApplicationFactory factory) : ApiIntegrationTestBase(factory)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters =
		{
			new JsonStringEnumConverter()
		}
	};

	[Fact]
	public async Task DeleteSeasonEvent_Admin_Succeeds_AndDeletesRow()
	{
		SetUser("league-delete-season-admin");
		var (leagueId, seasonId, eventId) = await CreateLeagueSeasonAndEventAsync("league-delete-season-admin", "Delete Season League", "Week 1", 1);

		var response = await Client.DeleteAsync($"/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}");

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
		var storedEvent = await DbContext.LeagueSeasonEvents.FindAsync(eventId);
		storedEvent.Should().BeNull();
	}

	[Fact]
	public async Task DeleteSeasonEvent_NonGovernanceMember_IsForbidden()
	{
		SetUser("league-delete-season-owner");
		var (leagueId, seasonId, eventId) = await CreateLeagueSeasonAndEventAsync("league-delete-season-owner", "Delete Season Forbidden League", "Week 1", 1);

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = "league-delete-season-member",
			Role = DataLeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetUser("league-delete-season-member");
		var response = await Client.DeleteAsync($"/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}");

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task DeleteSeasonEvent_LaunchedEvent_ReturnsConflict()
	{
		SetUser("league-delete-season-launched-admin");
		var (leagueId, seasonId, eventId) = await CreateLeagueSeasonAndEventAsync("league-delete-season-launched-admin", "Delete Season Conflict League", "Week 1", 1);

		var storedEvent = await DbContext.LeagueSeasonEvents.FindAsync(eventId);
		storedEvent.Should().NotBeNull();
		storedEvent!.LaunchedGameId = Guid.CreateVersion7();
		await DbContext.SaveChangesAsync();

		var response = await Client.DeleteAsync($"/api/v1/leagues/{leagueId}/seasons/{seasonId}/events/{eventId}");

		response.StatusCode.Should().Be(HttpStatusCode.Conflict);
	}

	[Fact]
	public async Task DeleteOneOffEvent_Admin_Succeeds_AndDeletesRow()
	{
		SetUser("league-delete-oneoff-admin");
		var (leagueId, eventId) = await CreateOneOffEventAsync("league-delete-oneoff-admin", "Delete One-Off League", "Friday Night");

		var response = await Client.DeleteAsync($"/api/v1/leagues/{leagueId}/events/one-off/{eventId}");

		response.StatusCode.Should().Be(HttpStatusCode.NoContent);
		var storedEvent = await DbContext.LeagueOneOffEvents.FindAsync(eventId);
		storedEvent.Should().BeNull();
	}

	[Fact]
	public async Task DeleteOneOffEvent_NonGovernanceMember_IsForbidden()
	{
		SetUser("league-delete-oneoff-owner");
		var (leagueId, eventId) = await CreateOneOffEventAsync("league-delete-oneoff-owner", "Delete One-Off Forbidden League", "Friday Night");

		DbContext.LeagueMembersCurrent.Add(new LeagueMemberCurrent
		{
			LeagueId = leagueId,
			UserId = "league-delete-oneoff-member",
			Role = DataLeagueRole.Member,
			IsActive = true,
			JoinedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		});
		await DbContext.SaveChangesAsync();

		SetUser("league-delete-oneoff-member");
		var response = await Client.DeleteAsync($"/api/v1/leagues/{leagueId}/events/one-off/{eventId}");

		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task DeleteOneOffEvent_LaunchedEvent_ReturnsConflict()
	{
		SetUser("league-delete-oneoff-launched-admin");
		var (leagueId, eventId) = await CreateOneOffEventAsync("league-delete-oneoff-launched-admin", "Delete One-Off Conflict League", "Friday Night");

		var storedEvent = await DbContext.LeagueOneOffEvents.FindAsync(eventId);
		storedEvent.Should().NotBeNull();
		storedEvent!.LaunchedGameId = Guid.CreateVersion7();
		await DbContext.SaveChangesAsync();

		var response = await Client.DeleteAsync($"/api/v1/leagues/{leagueId}/events/one-off/{eventId}");

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
			Name = "Spring 2028"
		});
		createSeasonResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var season = await createSeasonResponse.Content.ReadFromJsonAsync<CreateLeagueSeasonResponse>(JsonOptions);
		season.Should().NotBeNull();

		var createEventResponse = await PostAsync($"/api/v1/leagues/{league.LeagueId}/seasons/{season!.SeasonId}/events", new CreateLeagueSeasonEventRequest
		{
			Name = eventName,
			SequenceNumber = sequenceNumber,
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
}