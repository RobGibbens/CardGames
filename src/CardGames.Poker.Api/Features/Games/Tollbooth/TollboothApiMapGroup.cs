using CardGames.Poker.Api.Features.Games.Tollbooth.v1;

namespace CardGames.Poker.Api.Features.Games.Tollbooth;

[EndpointMapGroup]
public static class TollboothApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var tollbooth = app.NewVersionedApi("Tollbooth");
		tollbooth.MapV1();
	}
}
