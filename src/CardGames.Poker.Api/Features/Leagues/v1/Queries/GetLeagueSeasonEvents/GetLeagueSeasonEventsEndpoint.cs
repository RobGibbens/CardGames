using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueSeasonEvents;

public static class GetLeagueSeasonEventsEndpoint
{
	public static RouteGroupBuilder MapGetLeagueSeasonEvents(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}/seasons/{seasonId:guid}/events",
				async (Guid leagueId, Guid seasonId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new GetLeagueSeasonEventsQuery(leagueId, seasonId), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueSeasonEventsErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueSeasonEventsErrorCode.Forbidden => Results.Forbid(),
							GetLeagueSeasonEventsErrorCode.SeasonNotFound => Results.NotFound(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetLeagueSeasonEvents")
			.WithSummary("Get league season events")
			.WithDescription("Returns season-linked events for an active member of the league.")
			.Produces<IReadOnlyList<LeagueSeasonEventDto>>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.Produces(StatusCodes.Status404NotFound)
			.RequireAuthorization();

		return group;
	}
}