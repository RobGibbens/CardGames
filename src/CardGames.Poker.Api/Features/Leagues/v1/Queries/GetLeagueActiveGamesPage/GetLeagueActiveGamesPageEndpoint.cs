using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueActiveGamesPage;

public static class GetLeagueActiveGamesPageEndpoint
{
	public static RouteGroupBuilder MapGetLeagueActiveGamesPage(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}/active-games",
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

					var result = await mediator.Send(new GetLeagueActiveGamesPageQuery(leagueId, resolvedPageSize, resolvedPageNumber), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueActiveGamesPageErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueActiveGamesPageErrorCode.Forbidden => Results.Forbid(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetLeagueActiveGamesPage")
			.WithSummary("Get league active games page")
			.WithDescription("Returns a paged list of active launched league games for an active league member.")
			.Produces<LeagueActiveGamesPageDto>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.RequireAuthorization();

		return group;
	}
}