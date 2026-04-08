using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueSeasonEventsPage;

public static class GetLeagueSeasonEventsPageEndpoint
{
	public static RouteGroupBuilder MapGetLeagueSeasonEventsPage(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}/seasons/{seasonId:guid}/events/page",
				async (
					Guid leagueId,
					Guid seasonId,
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

					var result = await mediator.Send(new GetLeagueSeasonEventsPageQuery(leagueId, seasonId, resolvedPageSize, resolvedPageNumber), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueSeasonEventsPageErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueSeasonEventsPageErrorCode.Forbidden => Results.Forbid(),
							GetLeagueSeasonEventsPageErrorCode.SeasonNotFound => Results.NotFound(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetLeagueSeasonEventsPage")
			.WithSummary("Get league season events page")
			.WithDescription("Returns a paged list of season-linked events for an active member of the league.")
			.Produces<LeagueSeasonEventsPageDto>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.Produces(StatusCodes.Status404NotFound)
			.RequireAuthorization();

		return group;
	}
}