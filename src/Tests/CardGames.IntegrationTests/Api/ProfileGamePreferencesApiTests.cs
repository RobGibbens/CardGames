using System.Net;
using System.Net.Http.Json;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CardGames.IntegrationTests.Api;

public class ProfileGamePreferencesApiTests(ApiWebApplicationFactory factory) : ApiIntegrationTestBase(factory)
{
	private const string Endpoint = "/api/v1/profile/game-preferences";

	[Fact]
	public async Task GetThenUpdateThenGet_GamePreferences_PersistsForCurrentUser()
	{
		SetUser("prefs-user-1");

		var initial = await GetAsync<GamePreferencesDto>(Endpoint);

		initial.Should().NotBeNull();
		initial!.DefaultSmallBlind.Should().Be(1);
		initial.DefaultBigBlind.Should().Be(2);
		initial.DefaultAnte.Should().Be(5);
		initial.DefaultMinimumBet.Should().Be(10);

		var update = new UpdateGamePreferencesRequest
		{
			DefaultSmallBlind = 10,
			DefaultBigBlind = 20,
			DefaultAnte = 5,
			DefaultMinimumBet = 20
		};

		var putResponse = await Client.PutAsJsonAsync(Endpoint, update);
		putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

		var updated = await GetAsync<GamePreferencesDto>(Endpoint);

		updated.Should().NotBeNull();
		updated!.DefaultSmallBlind.Should().Be(10);
		updated.DefaultBigBlind.Should().Be(20);
		updated.DefaultAnte.Should().Be(5);
		updated.DefaultMinimumBet.Should().Be(20);
	}

	[Fact]
	public async Task UpdateGamePreferences_InvalidPayload_ReturnsBadRequestProblemDetails()
	{
		SetUser("prefs-user-invalid");

		var response = await Client.PutAsJsonAsync(Endpoint, new UpdateGamePreferencesRequest
		{
			DefaultSmallBlind = 10,
			DefaultBigBlind = 5,
			DefaultAnte = 5,
			DefaultMinimumBet = 20
		});

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
		problem.Should().NotBeNull();
		problem!.Status.Should().Be(400);
		problem.Errors.Should().ContainKey(nameof(UpdateGamePreferencesRequest.DefaultBigBlind));
	}

	private void SetUser(string userId)
	{
		Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserHeader);
		Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
	}
}
