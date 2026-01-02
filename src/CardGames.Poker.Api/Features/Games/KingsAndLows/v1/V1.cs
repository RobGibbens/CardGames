using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.AcknowledgePotMatch;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.CreateGame;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DeckDraw;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DrawCards;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.PerformShowdown;
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

		mapGroup
			.MapCreateGame()
			.MapStartHand()
			.MapDropOrStay()
			.MapDrawCards()
			.MapDeckDraw()
			.MapPerformShowdown()
			.MapAcknowledgePotMatch()
			;
	}
}
