using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.PerformShowdown;

public static class PerformShowdownEndpoint
{
	public static RouteGroupBuilder MapPerformShowdown(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/showdown",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new PerformShowdownCommand(gameId);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							PerformShowdownErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							PerformShowdownErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName($"Baseball{nameof(MapPerformShowdown).TrimPrefix("Map")}")
			.WithSummary("Perform Showdown")
			.WithDescription("Performs the showdown phase to evaluate hands and award pots.")
			.Produces<PerformShowdownSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict);

		return group;
	}
}
