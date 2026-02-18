using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueOneOffEvents;

public static class GetLeagueOneOffEventsEndpoint
{
	public static RouteGroupBuilder MapGetLeagueOneOffEvents(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}/events/one-off",
				async (Guid leagueId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new GetLeagueOneOffEventsQuery(leagueId), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueOneOffEventsErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueOneOffEventsErrorCode.Forbidden => Results.Forbid(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetLeagueOneOffEvents")
			.WithSummary("Get league one-off events")
			.WithDescription("Returns one-off events for an active member of the league.")
			.Produces<IReadOnlyList<LeagueOneOffEventDto>>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.RequireAuthorization();

		return group;
	}
}