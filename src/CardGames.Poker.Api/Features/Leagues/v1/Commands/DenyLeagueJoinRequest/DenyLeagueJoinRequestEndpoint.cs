using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Features.Leagues.v1;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.DenyLeagueJoinRequest;

public static class DenyLeagueJoinRequestEndpoint
{
	public static RouteGroupBuilder MapDenyLeagueJoinRequest(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/join-requests/{joinRequestId:guid}/deny",
				async (Guid leagueId, Guid joinRequestId, ModerateLeagueJoinRequestRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new DenyLeagueJoinRequestCommand(leagueId, joinRequestId, request), cancellationToken);

					return result.Match(
						success => Results.NoContent(),
						error => error.Code switch
						{
							DenyLeagueJoinRequestErrorCode.Unauthorized => Results.Unauthorized(),
							DenyLeagueJoinRequestErrorCode.Forbidden => Results.Forbid(),
							DenyLeagueJoinRequestErrorCode.JoinRequestNotFound => Results.NotFound(new { error.Message }),
							DenyLeagueJoinRequestErrorCode.InvalidState => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("DenyLeagueJoinRequest")
			.WithSummary("Deny league join request")
			.WithDescription("Denies a pending league join request without creating membership.")
			.Produces(StatusCodes.Status204NoContent)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.Produces(StatusCodes.Status429TooManyRequests)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.RequireRateLimiting(LeagueRateLimitPolicies.JoinAndRequestFlow)
			.RequireAuthorization();

		return group;
	}
}
