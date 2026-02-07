using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBettingAction;

public record ProcessBettingActionCommand(Guid GameId, BettingActionType ActionType, int Amount)
	: IRequest<OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>>, IGameStateChangingCommand;
