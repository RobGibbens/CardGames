using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CreateGame;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.JoinGame;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.UpdateTableSettings;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetCurrentBettingRound;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetCurrentDrawPlayer;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetCurrentPlayerTurn;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGamePlayers;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGames;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetHandHistory;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetTableSettings;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/five-card-draw")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name])
			.AddFluentValidationAutoValidation();

	mapGroup
			.MapCreateGame()
			.MapJoinGame()
			.MapStartHand()
			.MapCollectAntes()
			.MapDealHands()
			.MapProcessBettingAction()
			.MapProcessDraw()
			.MapPerformShowdown()
			.MapGetGames()
			.MapGetGamePlayers()
			.MapGetCurrentPlayerTurn()
			.MapGetCurrentDrawPlayer()
			.MapGetCurrentBettingRound()
			.MapGetHandHistory()
			.MapGetTableSettings()
			.MapUpdateTableSettings()
			;
	}
}