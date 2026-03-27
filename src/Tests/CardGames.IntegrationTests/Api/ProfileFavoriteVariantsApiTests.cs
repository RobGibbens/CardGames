using System.Net;
using System.Net.Http.Json;
using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Contracts;

namespace CardGames.IntegrationTests.Api;

public class ProfileFavoriteVariantsApiTests(ApiWebApplicationFactory factory) : ApiIntegrationTestBase(factory)
{
	private const string FavoriteVariantsEndpoint = "/api/v1/profile/favorite-variants";
	private const string GamePreferencesEndpoint = "/api/v1/profile/game-preferences";

	[Fact]
	public async Task GetThenUpdateThenGet_FavoriteVariants_PersistsForCurrentUser()
	{
		SetUser("favorite-variants-user-1");

		var initial = await GetAsync<FavoriteVariantsDto>(FavoriteVariantsEndpoint);

		initial.Should().NotBeNull();
		initial!.FavoriteVariantCodes.Should().BeEmpty();

		var update = new UpdateFavoriteVariantsRequest
		{
			FavoriteVariantCodes = ["holdem", "DEALERSCHOICE", "HoldEm"]
		};

		var putResponse = await Client.PutAsJsonAsync(FavoriteVariantsEndpoint, update);
		putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

		var updated = await GetAsync<FavoriteVariantsDto>(FavoriteVariantsEndpoint);

		updated.Should().NotBeNull();
		updated!.FavoriteVariantCodes.Should().Equal(["DEALERSCHOICE", "HOLDEM"]);
	}

	[Fact]
	public async Task UpdateFavoriteVariants_First_KeepsDefaultGamePreferencesStable()
	{
		SetUser("favorite-variants-user-2");

		var putResponse = await Client.PutAsJsonAsync(FavoriteVariantsEndpoint, new UpdateFavoriteVariantsRequest
		{
			FavoriteVariantCodes = ["HOLDEM"]
		});

		putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

		var preferences = await GetAsync<GamePreferencesDto>(GamePreferencesEndpoint);

		preferences.Should().NotBeNull();
		preferences!.DefaultSmallBlind.Should().Be(1);
		preferences.DefaultBigBlind.Should().Be(2);
		preferences.DefaultAnte.Should().Be(5);
		preferences.DefaultMinimumBet.Should().Be(10);
	}

	private void SetUser(string userId)
	{
		Client.DefaultRequestHeaders.Remove(TestAuthHandler.UserHeader);
		Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
	}
}