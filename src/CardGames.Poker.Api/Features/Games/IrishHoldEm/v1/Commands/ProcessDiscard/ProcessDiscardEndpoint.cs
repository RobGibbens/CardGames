using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;

/// <summary>
/// Endpoint for processing discard actions in an Irish Hold 'Em game.
/// </summary>
public static class ProcessDiscardEndpoint
{
	/// <summary>
	/// Maps the POST endpoint for processing discard actions.
	/// </summary>
	public static RouteGroupBuilder MapProcessDiscard(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/discard",
				async (Guid gameId, ProcessDiscardRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new ProcessDiscardCommand(gameId, request.DiscardIndices, request.PlayerSeatIndex);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							ProcessDiscardErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							ProcessDiscardErrorCode.NotInDiscardPhase => Results.Conflict(new { error.Message }),
							ProcessDiscardErrorCode.NotPlayerTurn => Results.Conflict(new { error.Message }),
							ProcessDiscardErrorCode.NoEligiblePlayers => Results.Conflict(new { error.Message }),
							ProcessDiscardErrorCode.InvalidDiscardCount => Results.UnprocessableEntity(new { error.Message }),
							ProcessDiscardErrorCode.InvalidCardIndex => Results.UnprocessableEntity(new { error.Message }),
							ProcessDiscardErrorCode.AlreadyDiscarded => Results.Conflict(new { error.Message }),
							ProcessDiscardErrorCode.InsufficientCards => Results.UnprocessableEntity(new { error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName("IrishHoldEmProcessDiscard")
			.WithSummary("Process Discard Action")
			.WithDescription(
				"Processes a discard action for the current player in Irish Hold 'Em, requiring them to " +
				"discard exactly 2 of their 4 hole cards after the flop betting round. No replacement cards " +
				"are dealt — players continue with their remaining 2 hole cards.\n\n" +
				"**Irish Hold 'Em discard rules:**\n" +
				"- Players must discard exactly 2 cards\n" +
				"- Card indices must be between 0 and 3 (inclusive)\n" +
				"- No replacement cards are dealt\n\n" +
				"**Phase Transitions:**\n" +
				"- After all players discard: transitions to Turn betting round\n" +
				"- Players who have folded are automatically skipped")
			.Produces<ProcessDiscardSuccessful>()
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status422UnprocessableEntity);

		return group;
	}
}
