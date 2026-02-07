using CardGames.Poker.Api.Features.Games.SevenCardStud.v1;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud;

[EndpointMapGroup]
public static class SevenCardStudApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var sevenCardStud = app.NewVersionedApi("SevenCardStud");
		sevenCardStud.MapV1();
	}
}
