using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueSeasons;

public static class GetLeagueSeasonsEndpoint
{
	public static RouteGroupBuilder MapGetLeagueSeasons(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}/seasons",
				async (Guid leagueId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new GetLeagueSeasonsQuery(leagueId), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueSeasonsErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueSeasonsErrorCode.Forbidden => Results.Forbid(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetLeagueSeasons")
			.WithSummary("Get league seasons")
			.WithDescription("Returns seasons for an active member of a league.")
			.Produces<IReadOnlyList<LeagueSeasonDto>>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.RequireAuthorization();

		return group;
	}
}