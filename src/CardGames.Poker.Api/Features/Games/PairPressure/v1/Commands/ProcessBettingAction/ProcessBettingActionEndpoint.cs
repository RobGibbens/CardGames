using CardGames.Poker.Api.Extensions;
using MediatR;
using SharedProcessBettingActionErrorCode = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction.ProcessBettingActionErrorCode;
using SharedProcessBettingActionRequest = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction.ProcessBettingActionRequest;
using SharedProcessBettingActionSuccessful = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction.ProcessBettingActionSuccessful;

namespace CardGames.Poker.Api.Features.Games.PairPressure.v1.Commands.ProcessBettingAction;

public static class ProcessBettingActionEndpoint
{
	public static RouteGroupBuilder MapProcessBettingAction(this RouteGroupBuilder group)
	{
		group.MapPost("{gameId:guid}/betting/actions",
				async (Guid gameId, SharedProcessBettingActionRequest request, IMediator mediator, CancellationToken cancellationToken) =>
				{
					var result = await mediator.Send(new ProcessBettingActionCommand(gameId, request.ActionType, request.Amount), cancellationToken);

					return result.Match(
						success => Results.Ok(success),
						error => error.Code switch
						{
							SharedProcessBettingActionErrorCode.GameNotFound => Results.NotFound(new { error.Message }),
							SharedProcessBettingActionErrorCode.InvalidGameState => Results.Conflict(new { error.Message }),
							SharedProcessBettingActionErrorCode.NoBettingRound => Results.Conflict(new { error.Message }),
							SharedProcessBettingActionErrorCode.NotPlayerTurn => Results.Conflict(new { error.Message }),
							SharedProcessBettingActionErrorCode.InvalidAction => Results.UnprocessableEntity(new { error.Message }),
							_ => Results.Problem(error.Message)
						}
					);
				})
			.WithName($"PairPressure{nameof(MapProcessBettingAction).TrimPrefix("Map")}")
			.WithSummary("Process Betting Action")
			.WithDescription("Processes a Pair Pressure betting action and advances the hand when the street is complete.")
			.Produces<SharedProcessBettingActionSuccessful>(StatusCodes.Status200OK)
			.ProducesProblem(StatusCodes.Status404NotFound)
			.ProducesProblem(StatusCodes.Status409Conflict)
			.ProducesProblem(StatusCodes.Status422UnprocessableEntity);

		return group;
	}
}