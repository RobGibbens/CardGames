using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.CreateGame;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/seven-card-stud")
			.HasApiVersion(1.0)
			.WithTags([Feature.Name])
			.AddFluentValidationAutoValidation();

		mapGroup
			.MapCreateGame();
	}
}
