using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetMyLeagues;

public static class GetMyLeaguesEndpoint
{
	public static RouteGroupBuilder MapGetMyLeagues(this RouteGroupBuilder group)
	{
		group.MapGet("mine",
				async (IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new GetMyLeaguesQuery(), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetMyLeaguesErrorCode.Unauthorized => Results.Unauthorized(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetMyLeagues")
			.WithSummary("Get my leagues")
			.WithDescription("Returns leagues where the current user is an active member.")
			.Produces<IReadOnlyList<LeagueSummaryDto>>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.RequireAuthorization();

		return group;
	}
}