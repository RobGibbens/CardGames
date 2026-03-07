using CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1;

namespace CardGames.Poker.Api.Features.Games.HoldTheBaseball;

[EndpointMapGroup]
public static class HoldTheBaseballApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var holdTheBaseball = app.NewVersionedApi("HoldTheBaseball");
		holdTheBaseball.MapV1();
	}
}
