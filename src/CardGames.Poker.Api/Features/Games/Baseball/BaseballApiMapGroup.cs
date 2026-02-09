using CardGames.Poker.Api.Features.Games.Baseball.v1;

namespace CardGames.Poker.Api.Features.Games.Baseball;

[EndpointMapGroup]
public static class BaseballApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var baseball = app.NewVersionedApi("Baseball");
		baseball.MapV1();
	}
}
