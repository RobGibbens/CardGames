using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games;
using CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.FoldDuringDraw;
using CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;

namespace CardGames.Poker.Api.Features.Games.IrishHoldEm.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/irish-hold-em")
			.HasApiVersion(1.0)
			.WithTags(["Irish Hold 'Em"]);

		var currentDrawCommandGroup = mapGroup.MapGroup(string.Empty)
			.RequireAuthorization()
			.RequireGameCurrentDrawPlayerAuthorization();

		currentDrawCommandGroup.MapProcessDiscard();
		currentDrawCommandGroup.MapFoldDuringDraw();
	}
}
