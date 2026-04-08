using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.DeleteLeagueSeasonEvent;

public static class DeleteLeagueSeasonEventEndpoint
{
	public static RouteGroupBuilder MapDeleteLeagueSeasonEvent(this RouteGroupBuilder group)
	{
		group.MapDelete("{leagueId:guid}/seasons/{seasonId:guid}/events/{eventId:guid}", async Task<IResult> (
			Guid leagueId,
			Guid seasonId,
			Guid eventId,
			IMediator mediator,
			CancellationToken cancellationToken) =>
		{
			var result = await mediator.Send(new DeleteLeagueSeasonEventCommand(leagueId, seasonId, eventId), cancellationToken);

			return result.Match<IResult>(
				_ => Results.NoContent(),
				error => error.Code switch
				{
					DeleteLeagueSeasonEventErrorCode.Unauthorized => Results.Unauthorized(),
					DeleteLeagueSeasonEventErrorCode.Forbidden => Results.Forbid(),
					DeleteLeagueSeasonEventErrorCode.LeagueNotFound => Results.NotFound(),
					DeleteLeagueSeasonEventErrorCode.SeasonNotFound => Results.NotFound(),
					DeleteLeagueSeasonEventErrorCode.EventNotFound => Results.NotFound(),
					DeleteLeagueSeasonEventErrorCode.Conflict => Results.Conflict(new { error.Message }),
					_ => Results.Problem(error.Message)
				});
		})
		.WithName("DeleteLeagueSeasonEvent")
		.WithSummary("Delete league season event")
		.WithDescription("Deletes a planned, unlaunched season event.")
		.Produces(StatusCodes.Status204NoContent)
		.Produces(StatusCodes.Status401Unauthorized)
		.Produces(StatusCodes.Status403Forbidden)
		.Produces(StatusCodes.Status404NotFound)
		.Produces(StatusCodes.Status409Conflict)
		.ProducesProblem(StatusCodes.Status500InternalServerError);

		return group;
	}
}