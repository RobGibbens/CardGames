using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueUpcomingEventsPage;

public static class GetLeagueUpcomingEventsPageEndpoint
{
	public static RouteGroupBuilder MapGetLeagueUpcomingEventsPage(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}/events/upcoming",
				async (
					Guid leagueId,
					IMediator mediator,
					int? pageSize = null,
					int? pageNumber = null,
					int? take = null,
					int? skip = null,
					CancellationToken cancellationToken = default) =>
				{
					var resolvedPageSize = Math.Clamp(pageSize ?? take ?? 5, 1, 100);
					var resolvedPageNumber = pageNumber ?? 1;

					if (!pageNumber.HasValue && skip.HasValue)
					{
						resolvedPageNumber = Math.Max(1, (skip.Value / resolvedPageSize) + 1);
					}

					var result = await mediator.Send(new GetLeagueUpcomingEventsPageQuery(leagueId, resolvedPageSize, resolvedPageNumber), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueUpcomingEventsPageErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueUpcomingEventsPageErrorCode.Forbidden => Results.Forbid(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetLeagueUpcomingEventsPage")
			.WithSummary("Get league upcoming events page")
			.WithDescription("Returns a paged list of upcoming league events for an active league member.")
			.Produces<LeagueUpcomingEventsPageDto>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.RequireAuthorization();

		return group;
	}
}