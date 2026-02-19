using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Features.Leagues.v1;
using CardGames.Poker.Api.Features.Leagues.v1.Telemetry;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Diagnostics;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.ApproveLeagueJoinRequest;

public static class ApproveLeagueJoinRequestEndpoint
{
	public static RouteGroupBuilder MapApproveLeagueJoinRequest(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/join-requests/{joinRequestId:guid}/approve",
				async (Guid leagueId, Guid joinRequestId, ModerateLeagueJoinRequestRequest request, IMediator mediator, LeaguesTelemetry telemetry, CancellationToken cancellationToken) =>
				{
					var started = Stopwatch.GetTimestamp();
					var result = await mediator.Send(new ApproveLeagueJoinRequestCommand(leagueId, joinRequestId, request), cancellationToken);

					var httpResult = result.Match(
						success => Results.NoContent(),
						error => error.Code switch
						{
							ApproveLeagueJoinRequestErrorCode.Unauthorized => Results.Unauthorized(),
							ApproveLeagueJoinRequestErrorCode.Forbidden => Results.Forbid(),
							ApproveLeagueJoinRequestErrorCode.JoinRequestNotFound => Results.NotFound(new { error.Message }),
							ApproveLeagueJoinRequestErrorCode.InvalidState => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});

					var statusCode = (httpResult as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;
					telemetry.RecordEndpointLatency("join_approve", statusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
					telemetry.RecordFunnelAttempt("approve", statusCode < 400 ? "success" : "failure");

					return httpResult;
				})
			.WithName("ApproveLeagueJoinRequest")
			.WithSummary("Approve league join request")
			.WithDescription("Approves a pending join request and activates membership.")
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
