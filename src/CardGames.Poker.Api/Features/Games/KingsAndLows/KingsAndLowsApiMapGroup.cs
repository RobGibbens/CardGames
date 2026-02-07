using CardGames.Poker.Api.Features.Games.KingsAndLows.v1;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows;

[EndpointMapGroup]
public static class KingsAndLowsApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var kingsAndLows = app.NewVersionedApi("KingsAndLows");
		kingsAndLows.MapV1();
	}
}
