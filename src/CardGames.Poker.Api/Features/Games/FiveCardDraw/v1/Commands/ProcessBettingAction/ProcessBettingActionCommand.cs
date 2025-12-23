using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;

/// <summary>
/// Command to process a betting action from the current player in a Five Card Draw game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="ActionType">The type of betting action to perform (check, bet, call, raise, fold, or all-in).</param>
/// <param name="Amount">The chip amount for bet or raise actions. Ignored for check, call, fold, and all-in actions.
/// For raises, this is the total amount to put in, not the raise increment.</param>
public record ProcessBettingActionCommand(
	Guid GameId,
	BettingActionType ActionType,
	int Amount = 0
) : IRequest<OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>>, IGameStateChangingCommand;

