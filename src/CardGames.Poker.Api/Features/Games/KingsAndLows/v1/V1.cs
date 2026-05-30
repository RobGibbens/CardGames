using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.AcknowledgePotMatch;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DeckDraw;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DrawCards;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.ResumeAfterChipCheck;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.StartHand;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/kings-and-lows")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name])
			.AddFluentValidationAutoValidation();

		var authenticatedGroup = mapGroup.MapGroup(string.Empty)
			.RequireAuthorization();

		var hostCommandGroup = authenticatedGroup.MapGroup(string.Empty)
			.RequireGameHostAuthorization();

		var playerCommandGroup = authenticatedGroup.MapGroup(string.Empty)
			.RequireCallerOwnsTargetPlayerAuthorization();

		var participantCommandGroup = authenticatedGroup.MapGroup(string.Empty)
			.RequireGameParticipantAuthorization();

		hostCommandGroup.MapStartHand();
		playerCommandGroup.MapDropOrStay();
		playerCommandGroup.MapDrawCards();
		playerCommandGroup.MapDeckDraw();
		participantCommandGroup.MapPerformShowdown();
		participantCommandGroup.MapAcknowledgePotMatch();
		hostCommandGroup.MapResumeAfterChipCheck();
	}
}
