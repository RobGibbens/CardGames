using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;
using SharedProcessBettingActionError = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction.ProcessBettingActionError;
using SharedProcessBettingActionSuccessful = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction.ProcessBettingActionSuccessful;

namespace CardGames.Poker.Api.Features.Games.PairPressure.v1.Commands.ProcessBettingAction;

public record ProcessBettingActionCommand(
	Guid GameId,
	BettingActionType ActionType,
	int Amount = 0
) : IRequest<OneOf<SharedProcessBettingActionSuccessful, SharedProcessBettingActionError>>, IGameStateChangingCommand;