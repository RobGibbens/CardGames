using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace CardGames.Poker.Api.Features.Games.HoldEm.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/hold-em")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name])
			.AddFluentValidationAutoValidation();

		var authenticatedGroup = mapGroup.MapGroup(string.Empty)
			.RequireAuthorization();

		var hostCommandGroup = authenticatedGroup.MapGroup(string.Empty)
			.RequireGameHostAuthorization();

		var currentPlayerCommandGroup = authenticatedGroup.MapGroup(string.Empty)
			.RequireGameCurrentPlayerAuthorization();

		var participantCommandGroup = authenticatedGroup.MapGroup(string.Empty)
			.RequireGameParticipantAuthorization();

		hostCommandGroup.MapStartHand();
		currentPlayerCommandGroup.MapProcessBettingAction();

		participantCommandGroup.MapPerformShowdown();
	}
}
