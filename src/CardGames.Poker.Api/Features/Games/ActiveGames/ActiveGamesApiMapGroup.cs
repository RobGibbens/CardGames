using CardGames.Poker.Api.Features.Games.ActiveGames.v1;

namespace CardGames.Poker.Api.Features.Games.ActiveGames;

[EndpointMapGroup]
public static class ActiveGamesApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var activeGames = app.NewVersionedApi("ActiveGames");
		activeGames.MapV1();
	}
}
