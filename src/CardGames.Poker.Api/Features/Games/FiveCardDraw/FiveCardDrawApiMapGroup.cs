using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw;

[EndpointMapGroup]
public static class FiveCardDrawApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var fiveCardDraw = app.NewVersionedApi("FiveCardDraw");
		fiveCardDraw.MapV1();
	}
}