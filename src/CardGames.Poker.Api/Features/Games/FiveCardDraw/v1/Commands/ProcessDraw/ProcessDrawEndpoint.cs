using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;

/// <summary>
/// Endpoint for processing draw actions in a Five Card Draw game.
/// </summary>
public static class ProcessDrawEndpoint
{
	/// <summary>
	/// Maps the POST endpoint for processing draw actions.
	/// </summary>
	public static RouteGroupBuilder MapProcessDraw(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/draw",
				async (Guid gameId, ProcessDrawRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new ProcessDrawCommand(gameId, request.DiscardIndices);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							ProcessDrawErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							ProcessDrawErrorCode.NotInDrawPhase => Results.Conflict(new { error.Message }),
							ProcessDrawErrorCode.NotPlayerTurn => Results.Conflict(new { error.Message }),
							ProcessDrawErrorCode.NoEligiblePlayers => Results.Conflict(new { error.Message }),
							ProcessDrawErrorCode.TooManyDiscards => Results.UnprocessableEntity(new { error.Message }),
							ProcessDrawErrorCode.InvalidCardIndex => Results.UnprocessableEntity(new { error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName($"FiveCardDraw{nameof(MapProcessDraw).TrimPrefix("Map")}")
			.WithSummary("Process Draw Action")
			.WithDescription(
				"Processes a draw action for the current player, allowing them to discard unwanted cards " +
				"and receive replacement cards from the deck. After all players have drawn, " +
				"the game automatically advances to the second betting round.\n\n" +
				"**Standard Five Card Draw rules apply:**\n" +
				"- Players may discard 0-3 cards\n" +
				"- Pass an empty array to \"stand pat\" (keep all cards)\n" +
				"- Discarded cards are replaced with new cards from the deck\n" +
				"- Card indices must be between 0 and 4 (inclusive)\n\n" +
				"**Phase Transitions:**\n" +
				"- After all players draw: transitions to second betting round\n" +
				"- Players who have folded or are all-in are automatically skipped")
			.Produces<ProcessDrawSuccessful>()
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status422UnprocessableEntity);

		return group;
	}
}
