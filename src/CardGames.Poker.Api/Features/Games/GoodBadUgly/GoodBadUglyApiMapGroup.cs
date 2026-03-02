using CardGames.Poker.Api.Features.Games.GoodBadUgly.v1;

namespace CardGames.Poker.Api.Features.Games.GoodBadUgly;

[EndpointMapGroup]
public static class GoodBadUglyApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var api = app.NewVersionedApi("GoodBadUgly");
		api.MapV1();
	}
}
