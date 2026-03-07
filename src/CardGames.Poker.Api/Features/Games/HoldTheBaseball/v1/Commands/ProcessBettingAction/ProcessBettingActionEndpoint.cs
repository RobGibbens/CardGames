using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using MediatR;

namespace CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1.Commands.ProcessBettingAction;

/// <summary>
/// Endpoint for processing betting actions in a Hold the Baseball game.
/// </summary>
public static class ProcessBettingActionEndpoint
{
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
			.WithName($"HoldTheBaseball{nameof(MapProcessBettingAction).TrimPrefix("Map")}")
			.WithSummary("Process Betting Action")
			.WithDescription("Processes a betting action in a Hold the Baseball game.")
			.Produces<ProcessBettingActionSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status422UnprocessableEntity);

		return group;
	}
}

public record ProcessBettingActionRequest
{
	public BettingActionType ActionType { get; init; }
	public int Amount { get; init; }
}
