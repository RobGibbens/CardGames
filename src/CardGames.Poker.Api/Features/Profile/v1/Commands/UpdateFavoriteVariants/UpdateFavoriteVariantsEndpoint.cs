using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Profile.v1.Commands.UpdateFavoriteVariants;

public static class UpdateFavoriteVariantsEndpoint
{
	public static RouteGroupBuilder MapUpdateFavoriteVariants(this RouteGroupBuilder group)
	{
		group.MapPut("favorite-variants",
				async (UpdateFavoriteVariantsRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(
						new UpdateFavoriteVariantsCommand(request.FavoriteVariantCodes),
						cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							UpdateFavoriteVariantsErrorCode.Unauthorized => Results.Unauthorized(),
							UpdateFavoriteVariantsErrorCode.InvalidFavoriteVariants => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("UpdateFavoriteVariants")
			.WithSummary("Update favorite variants")
			.WithDescription("Creates or updates favorite game variant codes for the authenticated player.")
			.Produces<FavoriteVariantsDto>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		return group;
	}
}