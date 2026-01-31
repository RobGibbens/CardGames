using CardGames.Poker.Api.Features.Games.Common.v1;

namespace CardGames.Poker.Api.Features.Games.Common;

[EndpointMapGroup]
public static class GamesApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var games = app.NewVersionedApi("Games");
		games.MapV1();
	}
}
