using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeasonEvent;

public static class CreateLeagueSeasonEventEndpoint
{
	public static RouteGroupBuilder MapCreateLeagueSeasonEvent(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/seasons/{seasonId:guid}/events",
				async (Guid leagueId, Guid seasonId, CreateLeagueSeasonEventRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new CreateLeagueSeasonEventCommand(leagueId, seasonId, request), cancellationToken);

					return result.Match(
						success => Results.Created($"/api/v1/leagues/{success.LeagueId}/seasons/{success.SeasonId}/events/{success.EventId}", success),
						error => error.Code switch
						{
							CreateLeagueSeasonEventErrorCode.Unauthorized => Results.Unauthorized(),
							CreateLeagueSeasonEventErrorCode.Forbidden => Results.Forbid(),
							CreateLeagueSeasonEventErrorCode.LeagueNotFound => Results.NotFound(),
							CreateLeagueSeasonEventErrorCode.SeasonNotFound => Results.NotFound(),
							CreateLeagueSeasonEventErrorCode.InvalidRequest => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("CreateLeagueSeasonEvent")
			.WithSummary("Create league season event")
			.WithDescription("Creates a season-linked event within a league.")
			.Produces<CreateLeagueSeasonEventResponse>(StatusCodes.Status201Created)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.Produces(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		return group;
	}
}