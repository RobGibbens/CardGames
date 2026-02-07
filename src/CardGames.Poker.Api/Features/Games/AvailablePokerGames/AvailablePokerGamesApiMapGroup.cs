using CardGames.Poker.Api.Features.Games.AvailablePokerGames.v1;

namespace CardGames.Poker.Api.Features.Games.AvailablePokerGames;

[EndpointMapGroup]
public static class AvailablePokerGamesApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var availablePokerGames = app.NewVersionedApi("AvailablePokerGames");
		availablePokerGames.MapV1();
	}
}
