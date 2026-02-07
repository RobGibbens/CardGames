using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe;

[EndpointMapGroup]
public static class TwosJacksManWithTheAxeApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var api = app.NewVersionedApi("TwosJacksManWithTheAxe");
		api.MapV1();
	}
}