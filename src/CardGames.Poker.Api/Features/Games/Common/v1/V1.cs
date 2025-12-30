using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGame;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGameRules;
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

		mapGroup.MapGetGame();
		mapGroup.MapGetGameRules();
	}
}
