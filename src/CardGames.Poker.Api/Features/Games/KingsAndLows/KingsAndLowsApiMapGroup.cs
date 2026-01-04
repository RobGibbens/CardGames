using CardGames.Poker.Api.Features.Games.KingsAndLows.v1;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows;

public static class KingsAndLowsApiMapGroup
{
	public static void AddKingsAndLowsEndpoints(this IEndpointRouteBuilder app)
	{
		var kingsAndLows = app.NewVersionedApi("KingsAndLows");
		kingsAndLows.MapV1();
	}
}
