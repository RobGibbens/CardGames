using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Features.Leagues.v1;
using CardGames.Poker.Api.Features.Leagues.v1.Telemetry;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Diagnostics;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.JoinLeague;

public static class JoinLeagueEndpoint
{
	public static RouteGroupBuilder MapJoinLeague(this RouteGroupBuilder group)
	{
		Task<IResult> Handler(JoinLeagueRequest request, IMediator mediator, LeaguesTelemetry telemetry, CancellationToken cancellationToken) =>
			HandleJoinAsync(request, mediator, telemetry, cancellationToken);

		group.MapPost("join",
				Handler)
			.WithName("JoinLeague")
			.WithSummary("Join league")
			.WithDescription("Joins a league with a valid invite token.")
			.Produces<JoinLeagueResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status429TooManyRequests)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireRateLimiting(LeagueRateLimitPolicies.JoinAndRequestFlow)
			.RequireAuthorization();

		group.MapPost("join-by-invite",
				Handler)
			.WithName("JoinLeagueByInvite")
			.WithSummary("Join league by invite")
			.WithDescription("Compatibility alias for joining a league with a valid invite token.")
			.Produces<JoinLeagueResponse>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status429TooManyRequests)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireRateLimiting(LeagueRateLimitPolicies.JoinAndRequestFlow)
			.RequireAuthorization();

		return group;
	}

	private static async Task<IResult> HandleJoinAsync(JoinLeagueRequest request, IMediator mediator, LeaguesTelemetry telemetry, CancellationToken cancellationToken)
	{
		var started = Stopwatch.GetTimestamp();
		var result = await mediator.Send(new JoinLeagueCommand(request), cancellationToken);

		var httpResult = result.Match(
			success => Results.Ok(success),
			error => error.Code switch
			{
				JoinLeagueErrorCode.Unauthorized => Results.Unauthorized(),
				JoinLeagueErrorCode.InvalidInvite => Results.BadRequest(new { error.Message }),
				JoinLeagueErrorCode.InviteRevoked => Results.BadRequest(new { error.Message }),
				JoinLeagueErrorCode.InviteExpired => Results.BadRequest(new { error.Message }),
				_ => Results.Problem(error.Message)
			});

		var statusCode = (httpResult as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;
		telemetry.RecordEndpointLatency("join_request", statusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
		telemetry.RecordFunnelAttempt("request", statusCode < 400 ? "success" : "failure");

		return httpResult;
	}
}
