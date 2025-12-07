using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw;

public static class FiveCardDrawApiMapGroup
{
	public static void AddFiveCardDrawEndpoints(this IEndpointRouteBuilder app)
	{
		var fiveCardDraw = app.NewVersionedApi("FiveCardDraw");
		fiveCardDraw.MapV1();
	}
}