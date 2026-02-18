using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueMembershipHistory;

public static class GetLeagueMembershipHistoryEndpoint
{
	public static RouteGroupBuilder MapGetLeagueMembershipHistory(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}/members/history",
				async (Guid leagueId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new GetLeagueMembershipHistoryQuery(leagueId), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueMembershipHistoryErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueMembershipHistoryErrorCode.Forbidden => Results.Forbid(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetLeagueMembershipHistory")
			.WithSummary("Get league membership history")
			.WithDescription("Returns membership timeline events for active members.")
			.Produces<IReadOnlyList<LeagueMembershipHistoryItemDto>>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.RequireAuthorization();

		return group;
	}
}
