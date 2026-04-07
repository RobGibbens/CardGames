using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetRecentCompletedLeagueSeasonEvents;

public static class GetRecentCompletedLeagueSeasonEventsEndpoint
{
	public static RouteGroupBuilder MapGetRecentCompletedLeagueSeasonEvents(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}/seasons/{seasonId:guid}/events/recent-completed",
				async (Guid leagueId, Guid seasonId, IMediator mediator, int? take = null, CancellationToken cancellationToken = default) =>
				{
					var result = await mediator.Send(new GetRecentCompletedLeagueSeasonEventsQuery(leagueId, seasonId, Math.Clamp(take ?? 5, 1, 20)), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetRecentCompletedLeagueSeasonEventsErrorCode.Unauthorized => Results.Unauthorized(),
							GetRecentCompletedLeagueSeasonEventsErrorCode.Forbidden => Results.Forbid(),
							GetRecentCompletedLeagueSeasonEventsErrorCode.SeasonNotFound => Results.NotFound(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetRecentCompletedLeagueSeasonEvents")
			.WithSummary("Get recent completed season events")
			.WithDescription("Returns the most recent completed events for a selected league season.")
			.Produces<IReadOnlyList<LeagueSeasonEventDto>>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.Produces(StatusCodes.Status404NotFound)
			.RequireAuthorization();

		return group;
	}
}