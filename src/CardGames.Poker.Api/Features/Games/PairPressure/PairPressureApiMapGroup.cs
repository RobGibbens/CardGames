using CardGames.Poker.Api.Features.Games.PairPressure.v1;

namespace CardGames.Poker.Api.Features.Games.PairPressure;

[EndpointMapGroup]
public static class PairPressureApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var pairPressure = app.NewVersionedApi("PairPressure");
		pairPressure.MapV1();
	}
}