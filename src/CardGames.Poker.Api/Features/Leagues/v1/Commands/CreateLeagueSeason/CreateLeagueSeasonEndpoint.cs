using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeason;

public static class CreateLeagueSeasonEndpoint
{
	public static RouteGroupBuilder MapCreateLeagueSeason(this RouteGroupBuilder group)
	{
		group.MapPost("{leagueId:guid}/seasons",
				async (Guid leagueId, CreateLeagueSeasonRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new CreateLeagueSeasonCommand(leagueId, request), cancellationToken);

					return result.Match(
						success => Results.Created($"/api/v1/leagues/{success.LeagueId}/seasons/{success.SeasonId}", success),
						error => error.Code switch
						{
							CreateLeagueSeasonErrorCode.Unauthorized => Results.Unauthorized(),
							CreateLeagueSeasonErrorCode.Forbidden => Results.Forbid(),
							CreateLeagueSeasonErrorCode.LeagueNotFound => Results.NotFound(),
							CreateLeagueSeasonErrorCode.InvalidRequest => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("CreateLeagueSeason")
			.WithSummary("Create league season")
			.WithDescription("Creates a new season container for a league.")
			.Produces<CreateLeagueSeasonResponse>(StatusCodes.Status201Created)
			.Produces(StatusCodes.Status401Unauthorized)
			.Produces(StatusCodes.Status403Forbidden)
			.Produces(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		return group;
	}
}