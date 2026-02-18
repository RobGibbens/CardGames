using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueStandings;

public static class GetLeagueStandingsEndpoint
{
	public static RouteGroupBuilder MapGetLeagueStandings(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}/standings",
				async (Guid leagueId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new GetLeagueStandingsQuery(leagueId), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueStandingsErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueStandingsErrorCode.Forbidden => Results.Forbid(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetLeagueStandings")
			.WithSummary("Get league standings")
			.WithDescription("Returns current league standings for active league members.")
			.Produces<IReadOnlyList<LeagueStandingEntryDto>>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.RequireAuthorization();

		return group;
	}
}
