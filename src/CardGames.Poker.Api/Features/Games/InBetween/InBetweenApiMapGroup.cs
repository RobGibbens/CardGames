using CardGames.Poker.Api.Features.Games.InBetween.v1;

namespace CardGames.Poker.Api.Features.Games.InBetween;

[EndpointMapGroup]
public static class InBetweenApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var inBetween = app.NewVersionedApi("InBetween");
		inBetween.MapV1();
	}
}
