using CardGames.Poker.Api.Features.Games.AvailablePokerGames.v1;

namespace CardGames.Poker.Api.Features.Games.AvailablePokerGames;

public static class AvailablePokerGamesApiMapGroup
{
	public static void AddAvailablePokerGamesEndpoints(this IEndpointRouteBuilder app)
	{
		var availablePokerGames = app.NewVersionedApi("AvailablePokerGames");
		availablePokerGames.MapV1();
	}
}
