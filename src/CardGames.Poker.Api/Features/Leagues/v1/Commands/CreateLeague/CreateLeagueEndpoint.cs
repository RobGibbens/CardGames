using CardGames.Poker.Api.Contracts;
using MediatR;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;

public static class CreateLeagueEndpoint
{
	public static RouteGroupBuilder MapCreateLeague(this RouteGroupBuilder group)
	{
		group.MapPost("",
				async (CreateLeagueRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new CreateLeagueCommand(request), cancellationToken);

					return result.Match(
						success => Results.Created($"/api/v1/leagues/{success.LeagueId}", success),
						error => error.Code switch
						{
							CreateLeagueErrorCode.Unauthorized => Results.Unauthorized(),
							CreateLeagueErrorCode.InvalidRequest => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						});
				})
			.WithName("CreateLeague")
			.WithSummary("Create league")
			.WithDescription("Creates a new league and adds the creator as an active admin member.")
			.Produces<CreateLeagueResponse>(StatusCodes.Status201Created)
			.Produces(StatusCodes.Status401Unauthorized)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.RequireAuthorization();

		return group;
	}
}