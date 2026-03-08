using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1.Commands.ProcessBettingAction;

/// <summary>
/// Command to process a betting action from the current player in a Hold the Baseball game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="ActionType">The type of betting action to perform.</param>
/// <param name="Amount">The chip amount for bet or raise actions.</param>
public record ProcessBettingActionCommand(
	Guid GameId,
	BettingActionType ActionType,
	int Amount = 0
) : IRequest<OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>>, IGameStateChangingCommand;
