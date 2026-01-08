using CardGames.Poker.Api.Features.Games.SevenCardStud.v1;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud;

public static class SevenCardStudApiMapGroup
{
	public static void AddSevenCardStudEndpoints(this IEndpointRouteBuilder app)
	{
		var sevenCardStud = app.NewVersionedApi("SevenCardStud");
		sevenCardStud.MapV1();
	}
}
