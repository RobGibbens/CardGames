using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CreateGame;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGame;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGames;
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
			.MapStartHand()
			.MapGetGames()
			.MapGetGame()
			//.MapGetCategoryById()
			//.MapAddCategory()
			//.MapUpdateCategory()
			;
	}
}