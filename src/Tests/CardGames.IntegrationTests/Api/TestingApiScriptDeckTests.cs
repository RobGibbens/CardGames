using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Api.Features.Testing;

namespace CardGames.IntegrationTests.Api;

public class TestingApiScriptDeckTests(ApiWebApplicationFactory factory) : ApiIntegrationTestBase(factory)
{
	[Fact]
	public async Task ScriptDeck_StoresAFullDeck_AndRejectsAnInvalidOne()
	{
		var gameId = Guid.CreateVersion7();
		var deck = FullDeck();

		var invalid = await Client.PostAsJsonAsync($"/api/v1/testing/games/{gameId}/deck", new { cards = deck.Take(51) });
		invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		var duplicate = deck.ToList();
		duplicate[51] = duplicate[0];
		var repeated = await Client.PostAsJsonAsync($"/api/v1/testing/games/{gameId}/deck", new { cards = duplicate });
		repeated.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		var unknown = deck.ToList();
		unknown[0] = "Xx";
		var badCard = await Client.PostAsJsonAsync($"/api/v1/testing/games/{gameId}/deck", new { cards = unknown });
		badCard.StatusCode.Should().Be(HttpStatusCode.BadRequest);

		var response = await Client.PostAsJsonAsync($"/api/v1/testing/games/{gameId}/deck", new { cards = deck });
		response.StatusCode.Should().Be(HttpStatusCode.NoContent);

		ScriptedDeckStore.Instance.TryConsume(gameId, out var stored).Should().BeTrue();
		stored.Should().HaveCount(52);
		stored[0].Suit.Should().Be(CardSuit.Hearts);
		stored[0].Symbol.Should().Be(CardSymbol.Deuce);
		stored[12].Symbol.Should().Be(CardSymbol.Ace);
		ScriptedDeckStore.Instance.TryConsume(gameId, out _).Should().BeFalse();
	}

	private static IReadOnlyList<string> FullDeck()
	{
		return
		[
			"2h", "3h", "4h", "5h", "6h", "7h", "8h", "9h", "Th", "Jh", "Qh", "Kh", "Ah",
			"2d", "3d", "4d", "5d", "6d", "7d", "8d", "9d", "Td", "Jd", "Qd", "Kd", "Ad",
			"2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "Ts", "Js", "Qs", "Ks", "As",
			"2c", "3c", "4c", "5c", "6c", "7c", "8c", "9c", "Tc", "Jc", "Qc", "Kc", "Ac"
		];
	}
}
