using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.AceChoice;
using CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.PlaceBet;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace CardGames.Poker.Api.Features.Games.InBetween.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/in-between")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name])
			.AddFluentValidationAutoValidation();

		mapGroup
			.MapAceChoice()
			.MapPlaceBet();
	}
}
