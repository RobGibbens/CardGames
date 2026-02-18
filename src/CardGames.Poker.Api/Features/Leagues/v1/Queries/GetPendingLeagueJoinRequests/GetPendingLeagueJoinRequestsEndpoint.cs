using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetPendingLeagueJoinRequests;

public static class GetPendingLeagueJoinRequestsEndpoint
{
	public static RouteGroupBuilder MapGetPendingLeagueJoinRequests(this RouteGroupBuilder group)
	{
		group.MapGet("{leagueId:guid}/join-requests/pending",
				async (Guid leagueId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new GetPendingLeagueJoinRequestsQuery(leagueId), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetPendingLeagueJoinRequestsErrorCode.Unauthorized => Results.Unauthorized(),
							GetPendingLeagueJoinRequestsErrorCode.Forbidden => Results.Forbid(),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("GetPendingLeagueJoinRequests")
			.WithSummary("Get pending league join requests")
			.WithDescription("Lists pending join requests for league managers and admins.")
			.Produces<IReadOnlyList<LeagueJoinRequestQueueItemDto>>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.RequireAuthorization();

		return group;
	}
}
