using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games;
using CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade;

namespace CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/screw-your-neighbor")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name]);

		var playerCommandGroup = mapGroup.MapGroup(string.Empty)
			.RequireAuthorization()
			.RequireCallerOwnsTargetPlayerAuthorization();

		playerCommandGroup
			.MapKeepOrTrade();
	}
}
