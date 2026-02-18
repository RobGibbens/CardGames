using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Contracts;
using FluentAssertions;

namespace CardGames.IntegrationTests.Api;

public class LeaguesApiMembershipHistoryTests(ApiWebApplicationFactory factory) : ApiIntegrationTestBase(factory)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters =
		{
			new JsonStringEnumConverter()
		}
	};

	[Fact]
	public async Task GetMembershipHistory_Endpoint_ReturnsEventsForActiveMember()
	{
		SetUser("league-history-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "API History League"
		});

		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var historyResponse = await Client.GetFromJsonAsync<IReadOnlyList<LeagueMembershipHistoryItemDto>>($"/api/v1/leagues/{league!.LeagueId}/members/history", JsonOptions);

		historyResponse.Should().NotBeNull();
		historyResponse!.Should().ContainSingle(x =>
			x.UserId == "league-history-admin" &&
			x.ActorUserId == "league-history-admin" &&
			x.EventType == CardGames.Poker.Api.Contracts.LeagueMembershipHistoryEventType.MemberJoined);
	}

	[Fact]
	public async Task GetMembershipHistory_Endpoint_ReturnsForbiddenForNonMember()
	{
		SetUser("league-history-owner");
		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "API History Forbidden League"
		});

		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		SetUser("league-history-outsider");
		var response = await Client.GetAsync($"/api/v1/leagues/{league!.LeagueId}/members/history");
		response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
	}

	private void SetUser(string userId)
	{
		Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserHeader);
		Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
	}
}
