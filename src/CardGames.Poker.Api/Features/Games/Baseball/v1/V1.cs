using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Queries.GetCurrentPlayerTurn;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/baseball")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name])
			.AddFluentValidationAutoValidation();

		var commandGroup = mapGroup.MapGroup(string.Empty)
			.RequireAuthorization();

		var hostCommandGroup = commandGroup.MapGroup(string.Empty)
			.RequireGameHostAuthorization();

		var currentPlayerCommandGroup = commandGroup.MapGroup(string.Empty)
			.RequireGameCurrentPlayerAuthorization();

		var playerCommandGroup = commandGroup.MapGroup(string.Empty)
			.RequireCallerOwnsTargetPlayerAuthorization();

		var participantCommandGroup = commandGroup.MapGroup(string.Empty)
			.RequireGameParticipantAuthorization();

		var currentPlayerQueryGroup = mapGroup.MapGroup(string.Empty)
			.RequireAuthorization()
			.RequireGameCurrentPlayerAuthorization();

		hostCommandGroup.MapStartHand();
		hostCommandGroup.MapCollectAntes();
		hostCommandGroup.MapDealHands();
		currentPlayerCommandGroup.MapProcessBettingAction();
		playerCommandGroup.MapProcessBuyCard();
		participantCommandGroup.MapPerformShowdown();

		currentPlayerQueryGroup.MapGetCurrentPlayerTurn();
	}
}
