using Asp.Versioning.Builder;
using CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace CardGames.Poker.Api.Features.Games.IrishHoldEm.v1;

public static class V1
{
	public static void MapV1(this IVersionedEndpointRouteBuilder app)
	{
		var mapGroup = app.MapGroup("/api/v{version:apiVersion}/games/irish-hold-em")
			.HasApiVersion(1.0)
			.WithTags(["Irish Hold 'Em"])
			.AddFluentValidationAutoValidation();

		mapGroup.MapProcessDiscard();
	}
}
