using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetGamePreferences;

public static class GetGamePreferencesEndpoint
{
	public static RouteGroupBuilder MapGetGamePreferences(this RouteGroupBuilder group)
	{
		group.MapGet("game-preferences",
				async (IMediator mediator, CancellationToken cancellationToken) =>
				{
					var response = await mediator.Send(new GetGamePreferencesQuery(), cancellationToken);
					return Results.Ok(response);
				})
			.WithName("GetGamePreferences")
			.WithSummary("Get game preferences")
			.WithDescription("Retrieves default blind, ante, and minimum bet preferences for the authenticated player.")
			.Produces<GamePreferencesDto>(StatusCodes.Status200OK)
			.RequireAuthorization();

		return group;
	}
}
