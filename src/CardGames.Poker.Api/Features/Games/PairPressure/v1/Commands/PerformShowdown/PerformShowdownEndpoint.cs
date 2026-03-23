using CardGames.Poker.Api.Extensions;
using MediatR;
using SharedPerformShowdownErrorCode = CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownErrorCode;
using SharedPerformShowdownSuccessful = CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownSuccessful;

namespace CardGames.Poker.Api.Features.Games.PairPressure.v1.Commands.PerformShowdown;

public static class PerformShowdownEndpoint
{
	public static RouteGroupBuilder MapPerformShowdown(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/showdown",
				async (Guid gameId, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new PerformShowdownCommand(gameId), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							SharedPerformShowdownErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							SharedPerformShowdownErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							SharedPerformShowdownErrorCode.NoValidHands => Results.BadRequest(new { error.Message }),
							SharedPerformShowdownErrorCode.UnsupportedGameType => Results.BadRequest(new { error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName($"PairPressure{nameof(MapPerformShowdown).TrimPrefix("Map")}")
			.WithSummary("Perform Showdown")
			.WithDescription("Performs Pair Pressure showdown and awards the hand using the dedicated Pair Pressure evaluation path.")
			.Produces<SharedPerformShowdownSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status400BadRequest)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict);

		return group;
	}
}