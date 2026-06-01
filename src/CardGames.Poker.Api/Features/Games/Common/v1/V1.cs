using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.ChooseDealerGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.DeleteGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.JoinGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.LeaveGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.ResolveJoinRequest;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleSitOut;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleOddsVisibility;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.UpdateTableSettings;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetCurrentDrawPlayer;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetTableSettings;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGamePlayers;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGames;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetHandHistory;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetPendingJoinRequestsForHost;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetRabbitHunt;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetCurrentBettingRound;
using CardGames.Poker.Api.Features.Games;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace CardGames.Poker.Api.Features.Games.Common.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name])
			.AddFluentValidationAutoValidation();

		var commandGroup = mapGroup.MapGroup(string.Empty)
			.RequireAuthorization();

		var hostCommandGroup = commandGroup.MapGroup(string.Empty)
			.RequireGameHostAuthorization();

		var playerCommandGroup = commandGroup.MapGroup(string.Empty)
			.RequireCallerOwnsTargetPlayerAuthorization();

		var authenticatedQueryGroup = mapGroup.MapGroup(string.Empty)
			.RequireAuthorization();

		var participantQueryGroup = authenticatedQueryGroup.MapGroup(string.Empty)
			.RequireGameParticipantAuthorization();

		var currentDrawPlayerQueryGroup = authenticatedQueryGroup.MapGroup(string.Empty)
			.RequireGameCurrentDrawPlayerAuthorization();

		//Commands
		playerCommandGroup.MapAddChips();
		commandGroup.MapChooseDealerGame();
		commandGroup.MapCreateGame();
		hostCommandGroup.MapDeleteGame();
		commandGroup.MapJoinGame();
		commandGroup.MapLeaveGame();
		hostCommandGroup.MapResolveJoinRequest();
		commandGroup.MapSitOut();
		hostCommandGroup.MapToggleOddsVisibility();
		hostCommandGroup.MapUpdateTableSettings();
		
		//Queries
		participantQueryGroup.MapGetCurrentBettingRound();
		currentDrawPlayerQueryGroup.MapGetCurrentDrawPlayer();
		mapGroup.MapGetGame();
		participantQueryGroup.MapGetGamePlayers();
		mapGroup.MapGetGameRules();
		mapGroup.MapGetGames();
		participantQueryGroup.MapGetHandHistory();
		authenticatedQueryGroup.MapGetPendingJoinRequestsForHost();
		participantQueryGroup.MapGetRabbitHunt();
		mapGroup.MapGetTableSettings();
	}
}
