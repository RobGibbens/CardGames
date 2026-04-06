using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.DeleteLeagueOneOffEvent;

public static class DeleteLeagueOneOffEventEndpoint
{
	public static RouteGroupBuilder MapDeleteLeagueOneOffEvent(this RouteGroupBuilder group)
	{
		group.MapDelete("{leagueId:guid}/events/one-off/{eventId:guid}", async Task<IResult> (
			Guid leagueId,
			Guid eventId,
			IMediator mediator,
			CancellationToken cancellationToken) =>
		{
			var result = await mediator.Send(new DeleteLeagueOneOffEventCommand(leagueId, eventId), cancellationToken);

			return result.Match<IResult>(
				_ => Results.NoContent(),
				error => error.Code switch
				{
					DeleteLeagueOneOffEventErrorCode.Unauthorized => Results.Unauthorized(),
					DeleteLeagueOneOffEventErrorCode.Forbidden => Results.Forbid(),
					DeleteLeagueOneOffEventErrorCode.LeagueNotFound => Results.NotFound(),
					DeleteLeagueOneOffEventErrorCode.EventNotFound => Results.NotFound(),
					DeleteLeagueOneOffEventErrorCode.Conflict => Results.Conflict(new { error.Message }),
					_ => Results.Problem(error.Message)
				});
		})
		.WithName("DeleteLeagueOneOffEvent")
		.WithSummary("Delete league one-off event")
		.WithDescription("Deletes a planned, unlaunched one-off event.")
		.Produces(StatusCodes.Status204NoContent)
		.Produces(StatusCodes.Status401Unauthorized)
		.Produces(StatusCodes.Status403Forbidden)
		.Produces(StatusCodes.Status404NotFound)
		.Produces(StatusCodes.Status409Conflict)
		.ProducesProblem(StatusCodes.Status500InternalServerError);

		return group;
	}
}