using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games;
using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.ProcessDraw;
using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetCurrentBettingRound;
using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetCurrentPlayerTurn;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/twos-jacks-man-with-the-axe")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name]);

		var commandGroup = mapGroup.MapGroup(string.Empty)
			.RequireAuthorization();

		var hostCommandGroup = commandGroup.MapGroup(string.Empty)
			.RequireGameHostAuthorization();

		var currentPlayerCommandGroup = commandGroup.MapGroup(string.Empty)
			.RequireGameCurrentPlayerAuthorization();

		var currentDrawCommandGroup = commandGroup.MapGroup(string.Empty)
			.RequireGameCurrentDrawPlayerAuthorization();

		var participantCommandGroup = commandGroup.MapGroup(string.Empty)
			.RequireGameParticipantAuthorization();

		var currentPlayerQueryGroup = mapGroup.MapGroup(string.Empty)
			.RequireAuthorization()
			.RequireGameCurrentPlayerAuthorization();

		var participantQueryGroup = mapGroup.MapGroup(string.Empty)
			.RequireAuthorization()
			.RequireGameParticipantAuthorization();

		hostCommandGroup.MapStartHand();
		hostCommandGroup.MapCollectAntes();
		hostCommandGroup.MapDealHands();
		currentPlayerCommandGroup.MapProcessBettingAction();
		currentDrawCommandGroup.MapProcessDraw();
		participantCommandGroup.MapPerformShowdown();

		currentPlayerQueryGroup.MapGetCurrentPlayerTurn();
		participantQueryGroup.MapGetCurrentBettingRound();
	}
}