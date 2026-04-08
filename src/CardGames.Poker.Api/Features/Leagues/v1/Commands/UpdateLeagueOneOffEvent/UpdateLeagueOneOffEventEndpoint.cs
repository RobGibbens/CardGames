using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.UpdateLeagueOneOffEvent;

public static class UpdateLeagueOneOffEventEndpoint
{
	public static RouteGroupBuilder MapUpdateLeagueOneOffEvent(this RouteGroupBuilder group)
	{
		group.MapPut("{leagueId:guid}/events/one-off/{eventId:guid}",
				async (Guid leagueId, Guid eventId, UpdateLeagueOneOffEventRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new UpdateLeagueOneOffEventCommand(leagueId, eventId, request), cancellationToken);

					return result.Match(
						success => Results.NoContent(),
						error => error.Code switch
						{
							UpdateLeagueOneOffEventErrorCode.Unauthorized => Results.Unauthorized(),
							UpdateLeagueOneOffEventErrorCode.Forbidden => Results.Forbid(),
							UpdateLeagueOneOffEventErrorCode.LeagueNotFound => Results.NotFound(new { error.Message }),
							UpdateLeagueOneOffEventErrorCode.EventNotFound => Results.NotFound(new { error.Message }),
							UpdateLeagueOneOffEventErrorCode.InvalidRequest => Results.BadRequest(new { error.Message }),
							UpdateLeagueOneOffEventErrorCode.Conflict => Results.Conflict(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("UpdateLeagueOneOffEvent")
			.WithSummary("Update league one-off event")
			.WithDescription("Updates a planned, unlaunched one-off event within a league.")
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