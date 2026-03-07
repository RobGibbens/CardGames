using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1.Commands.PerformShowdown;

/// <summary>
/// Endpoint for performing the showdown in a Hold the Baseball game.
/// </summary>
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
			.WithName($"HoldTheBaseball{nameof(MapPerformShowdown).TrimPrefix("Map")}")
			.WithSummary("Perform Showdown")
			.WithDescription(
				"Performs the showdown for a Hold the Baseball game. " +
				"Evaluates hands with 3s and 9s as wild cards (including community cards), " +
				"supporting Five of a Kind as the highest possible hand.")
			.Produces<PerformShowdownSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict);

		return group;
	}
}
