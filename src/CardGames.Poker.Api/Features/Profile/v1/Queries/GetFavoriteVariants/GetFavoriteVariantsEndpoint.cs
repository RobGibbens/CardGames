using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetFavoriteVariants;

public static class GetFavoriteVariantsEndpoint
{
	public static RouteGroupBuilder MapGetFavoriteVariants(this RouteGroupBuilder group)
	{
		group.MapGet("favorite-variants",
				async (IMediator mediator, CancellationToken cancellationToken) =>
				{
					var response = await mediator.Send(new GetFavoriteVariantsQuery(), cancellationToken);
					return Results.Ok(response);
				})
			.WithName("GetFavoriteVariants")
			.WithSummary("Get favorite variants")
			.WithDescription("Retrieves the authenticated player's favorited game variant codes.")
			.Produces<FavoriteVariantsDto>(StatusCodes.Status200OK)
			.RequireAuthorization();

		return group;
	}
}