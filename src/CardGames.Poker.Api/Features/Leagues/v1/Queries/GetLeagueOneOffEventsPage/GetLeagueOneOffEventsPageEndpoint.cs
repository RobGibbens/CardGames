using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueOneOffEventsPage;

public static class GetLeagueOneOffEventsPageEndpoint
{
	public static RouteGroupBuilder MapGetLeagueOneOffEventsPage(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}/events/one-off/page",
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

					var result = await mediator.Send(new GetLeagueOneOffEventsPageQuery(leagueId, resolvedPageSize, resolvedPageNumber), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueOneOffEventsPageErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueOneOffEventsPageErrorCode.Forbidden => Results.Forbid(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetLeagueOneOffEventsPage")
			.WithSummary("Get league one-off events page")
			.WithDescription("Returns a paged list of one-off events for an active member of the league.")
			.Produces<LeagueOneOffEventsPageDto>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.RequireAuthorization();

		return group;
	}
}