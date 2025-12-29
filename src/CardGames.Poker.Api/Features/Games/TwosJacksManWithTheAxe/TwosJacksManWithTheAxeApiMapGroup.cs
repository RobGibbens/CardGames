using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe;

public static class TwosJacksManWithTheAxeApiMapGroup
{
	public static void AddTwosJacksManWithTheAxeEndpoints(this IEndpointRouteBuilder app)
	{
		var api = app.NewVersionedApi("TwosJacksManWithTheAxe");
		api.MapV1();
	}
}