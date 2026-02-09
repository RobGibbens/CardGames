using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen;

[EndpointMapGroup]
public static class FollowTheQueenApiMapGroup
{
	public static void MapEndpoints(IEndpointRouteBuilder app)
	{
		var followTheQueen = app.NewVersionedApi("FollowTheQueen");
		followTheQueen.MapV1();
	}
}
