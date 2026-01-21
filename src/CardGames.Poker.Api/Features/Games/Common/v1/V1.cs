using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.DeleteGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.JoinGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.LeaveGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleSitOut;
using CardGames.Poker.Api.Features.Games.Common.v1.Commands.UpdateTableSettings;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetCurrentDrawPlayer;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetTableSettings;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGamePlayers;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGames;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetHandHistory;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetCurrentBettingRound;
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

		//Commands
		mapGroup.MapCreateGame();
		mapGroup.MapDeleteGame();
		mapGroup.MapJoinGame();
		mapGroup.MapLeaveGame();
		mapGroup.MapSitOut();
		mapGroup.MapUpdateTableSettings();
		
		//Queries
		mapGroup.MapGetCurrentBettingRound();
		mapGroup.MapGetCurrentDrawPlayer();
		mapGroup.MapGetGame();
		mapGroup.MapGetGamePlayers();
		mapGroup.MapGetGameRules();
		mapGroup.MapGetGames();
		mapGroup.MapGetHandHistory();
		mapGroup.MapGetHandHistoryWithPlayers();
		mapGroup.MapGetTableSettings();
	}
}
