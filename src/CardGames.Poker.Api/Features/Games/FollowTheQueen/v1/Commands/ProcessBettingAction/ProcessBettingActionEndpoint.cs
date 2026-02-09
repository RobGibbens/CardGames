using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.ProcessBettingAction;

/// <summary>
/// Endpoint for processing betting actions in a Follow the Queen game.
/// </summary>
public static class ProcessBettingActionEndpoint
{
	/// <summary>
	/// Maps the POST endpoint for processing betting actions.
	/// </summary>
	public static RouteGroupBuilder MapProcessBettingAction(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/betting/actions",
				async (Guid gameId, ProcessBettingActionRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var command = new ProcessBettingActionCommand(gameId, request.ActionType, request.Amount);
					var result = await mediator.Send(command, cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							ProcessBettingActionErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							ProcessBettingActionErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							ProcessBettingActionErrorCode.NoBettingRound => Results.Conflict(new { error.Message }),
							ProcessBettingActionErrorCode.NotPlayerTurn => Results.Conflict(new { error.Message }),
							ProcessBettingActionErrorCode.InvalidAction => Results.UnprocessableEntity(new { error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName($"FollowTheQueen{nameof(MapProcessBettingAction).TrimPrefix("Map")}")
			.WithSummary("Process Betting Action")
			.WithDescription(
				"Processes a betting action from the current player and advances the game state accordingly. " +
				"If the betting round completes (all players have acted and bets are equalized), " +
				"the game automatically advances to the next phase.\n\n" +
				"**Action Types:**\n" +
				"- `Check` (0): Pass without betting when no bet has been made\n" +
				"- `Bet` (1): Make an initial bet in the current betting round\n" +
				"- `Call` (2): Match the current highest bet\n" +
				"- `Raise` (3): Increase the current bet amount (amount is total to put in, not increment)\n" +
				"- `Fold` (4): Give up the hand\n" +
				"- `AllIn` (5): Bet all remaining chips\n\n" +
				"**Phase Transitions:**\n" +
				"- After first betting round completes: transitions to draw phase\n" +
				"- After second betting round completes: transitions to showdown\n" +
				"- If only one player remains (all others folded): transitions directly to showdown")
			.Produces<ProcessBettingActionSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status422UnprocessableEntity);

		return group;
	}
}

/// <summary>
/// Request body for processing a betting action.
/// </summary>
public record ProcessBettingActionRequest
{
	/// <summary>
	/// The type of betting action to perform.
	/// </summary>
	/// <remarks>
	/// Valid values: Check (0), Bet (1), Call (2), Raise (3), Fold (4), AllIn (5)
	/// </remarks>
	public BettingActionType ActionType { get; init; }

	/// <summary>
	/// The chip amount for bet or raise actions.
	/// </summary>
	/// <remarks>
	/// Ignored for check, call, fold, and all-in actions.
	/// For raises, this is the total amount to put in, not the raise increment.
	/// </remarks>
	public int Amount { get; init; }
}
