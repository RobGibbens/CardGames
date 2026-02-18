using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueDetail;

public static class GetLeagueDetailEndpoint
{
	public static RouteGroupBuilder MapGetLeagueDetail(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}",
				async (Guid leagueId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new GetLeagueDetailQuery(leagueId), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueDetailErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueDetailErrorCode.Forbidden => Results.Forbid(),
							GetLeagueDetailErrorCode.NotFound => Results.NotFound(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetLeagueDetail")
			.WithSummary("Get league detail")
			.WithDescription("Returns league detail for active members.")
			.Produces<LeagueDetailDto>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.RequireAuthorization();

		return group;
	}
}