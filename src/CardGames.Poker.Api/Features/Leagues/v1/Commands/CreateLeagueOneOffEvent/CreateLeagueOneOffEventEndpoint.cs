using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueOneOffEvent;

public static class CreateLeagueOneOffEventEndpoint
{
	public static RouteGroupBuilder MapCreateLeagueOneOffEvent(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/events/one-off",
				async (Guid leagueId, CreateLeagueOneOffEventRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new CreateLeagueOneOffEventCommand(leagueId, request), cancellationToken);

					return result.Match(
						success => Results.Created($"/api/v1/leagues/{success.LeagueId}/events/one-off/{success.EventId}", success),
						error => error.Code switch
						{
							CreateLeagueOneOffEventErrorCode.Unauthorized => Results.Unauthorized(),
							CreateLeagueOneOffEventErrorCode.Forbidden => Results.Forbid(),
							CreateLeagueOneOffEventErrorCode.LeagueNotFound => Results.NotFound(),
							CreateLeagueOneOffEventErrorCode.InvalidRequest => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("CreateLeagueOneOffEvent")
			.WithSummary("Create league one-off event")
			.WithDescription("Creates a one-off event within a league.")
			.Produces<CreateLeagueOneOffEventResponse>(StatusCodes.Status201Created)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.Produces(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		return group;
	}
}