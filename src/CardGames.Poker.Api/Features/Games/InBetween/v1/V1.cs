using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games;
using CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.AceChoice;
using CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.PlaceBet;

namespace CardGames.Poker.Api.Features.Games.InBetween.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/in-between")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name]);

		var playerCommandGroup = mapGroup.MapGroup(string.Empty)
			.RequireAuthorization()
			.RequireCallerOwnsTargetPlayerAuthorization();

		playerCommandGroup
			.MapAceChoice()
			.MapPlaceBet();
	}
}
