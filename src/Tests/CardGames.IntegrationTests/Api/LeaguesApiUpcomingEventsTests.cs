using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Refit;
using ContractLeagueOneOffEventType = CardGames.Poker.Api.Contracts.LeagueOneOffEventType;

namespace CardGames.IntegrationTests.Api;

public class LeaguesApiUpcomingEventsTests(ApiWebApplicationFactory factory) : ApiIntegrationTestBase(factory)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters =
		{
			new JsonStringEnumConverter()
		}
	};

	[Fact]
	public async Task GetUpcomingEventsPage_ReturnsPlannedOneOffEvents()
	{
		SetUser("league-upcoming-oneoff-admin");
		var (leagueId, eventId) = await CreateOneOffEventAsync("league-upcoming-oneoff-admin", "Upcoming One-Off League", "Friday Night Feature");

		var response = await Client.GetAsync($"/api/v1/leagues/{leagueId}/events/upcoming?pageSize=5&pageNumber=1");

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		var page = await response.Content.ReadFromJsonAsync<LeagueUpcomingEventsPageDto>(JsonOptions);
		page.Should().NotBeNull();
		page!.TotalCount.Should().Be(1);
		page.Entries.Should().HaveCount(1);
		page.Entries[0].OneOffEvent.Should().NotBeNull();
		page.Entries[0].SeasonEvent.Should().BeNull();
		page.Entries[0].OneOffEvent!.EventId.Should().Be(eventId);
		page.Entries[0].OneOffEvent.Name.Should().Be("Friday Night Feature");
	}

	[Fact]
	public async Task GetUpcomingEventsPage_RefitClient_DeserializesPlannedOneOffEvents()
	{
		SetUser("league-upcoming-refit-admin");
		var (leagueId, eventId) = await CreateOneOffEventAsync("league-upcoming-refit-admin", "Upcoming Refit League", "Refit Friday Night");

		var api = RestService.For<ILeaguesApi>(Client, new RefitSettings
		{
			ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)
			{
				Converters =
				{
					new JsonStringEnumConverter()
				}
			})
		});

		var response = await api.GetUpcomingEventsPageAsync(leagueId, pageSize: 5, pageNumber: 1);

		response.IsSuccessStatusCode.Should().BeTrue();
		response.Content.Should().NotBeNull();
		response.Content!.TotalCount.Should().Be(1);
		response.Content.Entries.Should().HaveCount(1);
		response.Content.Entries[0].OneOffEvent.Should().NotBeNull();
		response.Content.Entries[0].OneOffEvent!.EventId.Should().Be(eventId);
	}

	[Fact]
	public async Task GetUpcomingEventsPage_ExcludesLaunchedOneOffEventsThatAppearInActiveGames()
	{
		SetUser("league-upcoming-launch-admin");
		var (leagueId, eventId) = await CreateOneOffEventAsync("league-upcoming-launch-admin", "Upcoming Launch League", "Launch Me");

		var launchResponse = await PostAsync($"/api/v1/leagues/{leagueId}/events/one-off/{eventId}/launch", new LaunchLeagueEventSessionRequest
		{
			GameCode = "HOLDEM",
			HostStartingChips = 300
		});
		launchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

		var activeGamesResponse = await Client.GetAsync($"/api/v1/leagues/{leagueId}/active-games?pageSize=5&pageNumber=1");
		activeGamesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var activeGamesPage = await activeGamesResponse.Content.ReadFromJsonAsync<LeagueActiveGamesPageDto>(JsonOptions);
		activeGamesPage.Should().NotBeNull();
		activeGamesPage!.Entries.Should().ContainSingle(entry => entry.Name == "Launch Me");

		var upcomingResponse = await Client.GetAsync($"/api/v1/leagues/{leagueId}/events/upcoming?pageSize=5&pageNumber=1");
		upcomingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

		var upcomingPage = await upcomingResponse.Content.ReadFromJsonAsync<LeagueUpcomingEventsPageDto>(JsonOptions);
		upcomingPage.Should().NotBeNull();
		upcomingPage!.TotalCount.Should().Be(0);
		upcomingPage.Entries.Should().BeEmpty();
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