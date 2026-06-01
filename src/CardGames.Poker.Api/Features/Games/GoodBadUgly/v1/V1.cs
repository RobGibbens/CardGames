using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games;
using CardGames.Poker.Api.Features.Games.GoodBadUgly.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.GoodBadUgly.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.GoodBadUgly.v1.Commands.ProcessBettingAction;

namespace CardGames.Poker.Api.Features.Games.GoodBadUgly.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/good-bad-ugly")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name]);

		var authenticatedGroup = mapGroup.MapGroup(string.Empty)
			.RequireAuthorization();

		var hostCommandGroup = authenticatedGroup.MapGroup(string.Empty)
			.RequireGameHostAuthorization();

		var currentPlayerCommandGroup = authenticatedGroup.MapGroup(string.Empty)
			.RequireGameCurrentPlayerAuthorization();

		var participantCommandGroup = authenticatedGroup.MapGroup(string.Empty)
			.RequireGameParticipantAuthorization();

		hostCommandGroup.MapDealHands();
		currentPlayerCommandGroup.MapProcessBettingAction();
		participantCommandGroup.MapPerformShowdown();
	}
}
