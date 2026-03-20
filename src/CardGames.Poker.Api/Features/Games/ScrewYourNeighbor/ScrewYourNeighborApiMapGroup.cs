using CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1;

namespace CardGames.Poker.Api.Features.Games.ScrewYourNeighbor;

[EndpointMapGroup]
public static class ScrewYourNeighborApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var screwYourNeighbor = app.NewVersionedApi("ScrewYourNeighbor");
		screwYourNeighbor.MapV1();
	}
}
