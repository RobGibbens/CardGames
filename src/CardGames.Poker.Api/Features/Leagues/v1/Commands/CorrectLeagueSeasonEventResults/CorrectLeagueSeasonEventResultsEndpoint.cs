using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CorrectLeagueSeasonEventResults;

public static class CorrectLeagueSeasonEventResultsEndpoint
{
	public static RouteGroupBuilder MapCorrectLeagueSeasonEventResults(this RouteGroupBuilder group)
	{
		group.MapPut("{leagueId:guid}/seasons/{seasonId:guid}/events/{eventId:guid}/results/corrections",
				async (Guid leagueId, Guid seasonId, Guid eventId, CorrectLeagueSeasonEventResultsRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new CorrectLeagueSeasonEventResultsCommand(leagueId, seasonId, eventId, request), cancellationToken);

					return result.Match(
						success => Results.NoContent(),
						error => error.Code switch
						{
							CorrectLeagueSeasonEventResultsErrorCode.Unauthorized => Results.Unauthorized(),
							CorrectLeagueSeasonEventResultsErrorCode.Forbidden => Results.Forbid(),
							CorrectLeagueSeasonEventResultsErrorCode.LeagueNotFound => Results.NotFound(new { error.Message }),
							CorrectLeagueSeasonEventResultsErrorCode.SeasonNotFound => Results.NotFound(new { error.Message }),
							CorrectLeagueSeasonEventResultsErrorCode.EventNotFound => Results.NotFound(new { error.Message }),
							CorrectLeagueSeasonEventResultsErrorCode.MismatchedLeagueOrSeason => Results.BadRequest(new { error.Message }),
							CorrectLeagueSeasonEventResultsErrorCode.MemberNotFound => Results.NotFound(new { error.Message }),
							CorrectLeagueSeasonEventResultsErrorCode.ResultsNotIngested => Results.Conflict(new { error.Message }),
							CorrectLeagueSeasonEventResultsErrorCode.InvalidRequest => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("CorrectLeagueSeasonEventResults")
			.WithSummary("Correct league season event results")
			.WithDescription("Replaces existing season event results with corrected entries, updates standings, and writes an audit record.")
			.Produces(StatusCodes.Status204NoContent)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.RequireAuthorization();

		return group;
	}
}
