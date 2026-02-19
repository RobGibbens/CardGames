using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Features.Leagues.v1.Telemetry;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Diagnostics;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueJoinPreview;

public static class GetLeagueJoinPreviewEndpoint
{
	public static RouteGroupBuilder MapGetLeagueJoinPreview(this RouteGroupBuilder group)
	{
		group.MapGet("join-preview",
				async (string token, IMediator mediator, LeaguesTelemetry telemetry, CancellationToken cancellationToken) =>
				{
					var started = Stopwatch.GetTimestamp();
					var result = await mediator.Send(new GetLeagueJoinPreviewQuery(token), cancellationToken);

					var httpResult = result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							GetLeagueJoinPreviewErrorCode.Unauthorized => Results.Unauthorized(),
							GetLeagueJoinPreviewErrorCode.InvalidInvite => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});

					var statusCode = (httpResult as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;
					telemetry.RecordEndpointLatency("join_preview", statusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
					telemetry.RecordFunnelAttempt("join", statusCode < 400 ? "success" : "failure");

					return httpResult;
				})
			.WithName("GetLeagueJoinPreview")
			.WithSummary("Get league join preview")
			.WithDescription("Returns trust preview details for a league invite code.")
			.Produces<LeagueJoinPreviewDto>(StatusCodes.Status200OK)
			.Produces(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		return group;
	}
}
