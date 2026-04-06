using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.UpdateLeagueSeasonEvent;

public static class UpdateLeagueSeasonEventEndpoint
{
	public static RouteGroupBuilder MapUpdateLeagueSeasonEvent(this RouteGroupBuilder group)
	{
		group.MapPut("{leagueId:guid}/seasons/{seasonId:guid}/events/{eventId:guid}",
				async (Guid leagueId, Guid seasonId, Guid eventId, UpdateLeagueSeasonEventRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new UpdateLeagueSeasonEventCommand(leagueId, seasonId, eventId, request), cancellationToken);

					return result.Match(
						success => Results.NoContent(),
						error => error.Code switch
						{
							UpdateLeagueSeasonEventErrorCode.Unauthorized => Results.Unauthorized(),
							UpdateLeagueSeasonEventErrorCode.Forbidden => Results.Forbid(),
							UpdateLeagueSeasonEventErrorCode.LeagueNotFound => Results.NotFound(new { error.Message }),
							UpdateLeagueSeasonEventErrorCode.SeasonNotFound => Results.NotFound(new { error.Message }),
							UpdateLeagueSeasonEventErrorCode.EventNotFound => Results.NotFound(new { error.Message }),
							UpdateLeagueSeasonEventErrorCode.InvalidRequest => Results.BadRequest(new { error.Message }),
							UpdateLeagueSeasonEventErrorCode.Conflict => Results.Conflict(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("UpdateLeagueSeasonEvent")
			.WithSummary("Update league season event")
			.WithDescription("Updates a planned, unlaunched season event within a league.")
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