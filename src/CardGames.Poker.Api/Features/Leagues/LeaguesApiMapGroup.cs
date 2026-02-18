using CardGames.Poker.Api.Features.Leagues.v1;

namespace CardGames.Poker.Api.Features.Leagues;

[EndpointMapGroup]
public static class LeaguesApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var leagues = app.NewVersionedApi("Leagues");
		leagues.MapV1();
	}
}