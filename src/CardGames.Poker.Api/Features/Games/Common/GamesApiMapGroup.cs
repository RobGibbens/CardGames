using CardGames.Poker.Api.Features.Games.Common.v1;

namespace CardGames.Poker.Api.Features.Games.Common;

public static class GamesApiMapGroup
{
	public static void AddGamesEndpoints(this IEndpointRouteBuilder app)
	{
		var games = app.NewVersionedApi("Games");
		games.MapV1();
	}
}
