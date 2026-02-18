using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Contracts;
using FluentAssertions;

namespace CardGames.IntegrationTests.Api;

public class LeaguesApiInviteTests(ApiWebApplicationFactory factory) : ApiIntegrationTestBase(factory)
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters =
		{
			new JsonStringEnumConverter()
		}
	};

	[Fact]
	public async Task RevokeInvite_Endpoint_RevokesActiveInvite()
	{
		SetUser("league-api-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "API Revoke League"
		});

		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createInviteResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});

		createInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		invite.Should().NotBeNull();

		var revokeResponse = await Client.PostAsync($"/api/v1/leagues/{league.LeagueId}/invites/{invite!.InviteId}/revoke", content: null);
		revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

		var invitesResponse = await Client.GetFromJsonAsync<IReadOnlyList<LeagueInviteSummaryDto>>($"/api/v1/leagues/{league.LeagueId}/invites", JsonOptions);
		invitesResponse.Should().NotBeNull();
		invitesResponse!.Should().ContainSingle(x => x.InviteId == invite.InviteId && x.Status == CardGames.Poker.Api.Contracts.LeagueInviteStatus.Revoked);
	}

	[Fact]
	public async Task JoinByInviteAlias_Endpoint_JoinsLeague()
	{
		SetUser("league-api-admin");

		var createLeagueResponse = await PostAsync("/api/v1/leagues", new CreateLeagueRequest
		{
			Name = "API Join Alias League"
		});
		createLeagueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
		var league = await createLeagueResponse.Content.ReadFromJsonAsync<CreateLeagueResponse>(JsonOptions);
		league.Should().NotBeNull();

		var createInviteResponse = await PostAsync($"/api/v1/leagues/{league!.LeagueId}/invites", new CreateLeagueInviteRequest
		{
			ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(2)
		});
		createInviteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var invite = await createInviteResponse.Content.ReadFromJsonAsync<CreateLeagueInviteResponse>(JsonOptions);
		invite.Should().NotBeNull();

		var token = invite!.InviteUrl.Split('/').Last();
		SetUser("league-api-member");

		var joinResponse = await PostAsync("/api/v1/leagues/join-by-invite", new JoinLeagueRequest
		{
			Token = token
		});

		joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
		var joinResult = await joinResponse.Content.ReadFromJsonAsync<JoinLeagueResponse>(JsonOptions);
		joinResult.Should().NotBeNull();
		joinResult!.LeagueId.Should().Be(league.LeagueId);
		joinResult.Joined.Should().BeTrue();
	}

	private void SetUser(string userId)
	{
		Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserHeader);
		Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
	}
}
