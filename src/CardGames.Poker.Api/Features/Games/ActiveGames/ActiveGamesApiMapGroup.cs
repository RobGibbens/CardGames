using CardGames.Poker.Api.Features.Games.ActiveGames.v1;

namespace CardGames.Poker.Api.Features.Games.ActiveGames;

public static class ActiveGamesApiMapGroup
{
	public static void AddActiveGamesEndpoints(this IEndpointRouteBuilder app)
	{
		var activeGames = app.NewVersionedApi("ActiveGames");
		activeGames.MapV1();
	}
}
