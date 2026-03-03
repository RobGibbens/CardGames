using CardGames.Poker.Api.Features.Games.Generic.v1;

namespace CardGames.Poker.Api.Features.Games.Generic;

[EndpointMapGroup]
public static class GenericApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var generic = app.NewVersionedApi("Generic Games");
		generic.MapV1();
	}
}
