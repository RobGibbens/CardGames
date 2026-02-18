using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;

public static class IngestLeagueSeasonEventResultsEndpoint
{
	public static RouteGroupBuilder MapIngestLeagueSeasonEventResults(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/seasons/{seasonId:guid}/events/{eventId:guid}/results",
				async (Guid leagueId, Guid seasonId, Guid eventId, IngestLeagueSeasonEventResultsRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new IngestLeagueSeasonEventResultsCommand(leagueId, seasonId, eventId, request), cancellationToken);

					return result.Match(
						success => Results.NoContent(),
						error => error.Code switch
						{
							IngestLeagueSeasonEventResultsErrorCode.Unauthorized => Results.Unauthorized(),
							IngestLeagueSeasonEventResultsErrorCode.Forbidden => Results.Forbid(),
							IngestLeagueSeasonEventResultsErrorCode.LeagueNotFound => Results.NotFound(new { error.Message }),
							IngestLeagueSeasonEventResultsErrorCode.SeasonNotFound => Results.NotFound(new { error.Message }),
							IngestLeagueSeasonEventResultsErrorCode.EventNotFound => Results.NotFound(new { error.Message }),
							IngestLeagueSeasonEventResultsErrorCode.MismatchedLeagueOrSeason => Results.BadRequest(new { error.Message }),
							IngestLeagueSeasonEventResultsErrorCode.MemberNotFound => Results.NotFound(new { error.Message }),
							IngestLeagueSeasonEventResultsErrorCode.ResultsAlreadyIngested => Results.Conflict(new { error.Message }),
							IngestLeagueSeasonEventResultsErrorCode.InvalidRequest => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("IngestLeagueSeasonEventResults")
			.WithSummary("Ingest league season event results")
			.WithDescription("Ingests ranked member results for a season event and updates league standings.")
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
