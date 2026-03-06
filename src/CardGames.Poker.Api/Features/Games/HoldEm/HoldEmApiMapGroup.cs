using CardGames.Poker.Api.Features.Games.HoldEm.v1;

namespace CardGames.Poker.Api.Features.Games.HoldEm;

[EndpointMapGroup]
public static class HoldEmApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var holdEm = app.NewVersionedApi("HoldEm");
		holdEm.MapV1();
	}
}
