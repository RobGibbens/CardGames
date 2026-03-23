using MediatR;
using OneOf;
using SharedProcessBettingActionCommand = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction.ProcessBettingActionCommand;
using SharedProcessBettingActionError = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction.ProcessBettingActionError;
using SharedProcessBettingActionSuccessful = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.ProcessBettingAction.ProcessBettingActionSuccessful;

namespace CardGames.Poker.Api.Features.Games.PairPressure.v1.Commands.ProcessBettingAction;

public sealed class ProcessBettingActionCommandHandler(
	IRequestHandler<SharedProcessBettingActionCommand, OneOf<SharedProcessBettingActionSuccessful, SharedProcessBettingActionError>> innerHandler)
	: IRequestHandler<ProcessBettingActionCommand, OneOf<SharedProcessBettingActionSuccessful, SharedProcessBettingActionError>>
{
	public Task<OneOf<SharedProcessBettingActionSuccessful, SharedProcessBettingActionError>> Handle(
		ProcessBettingActionCommand command,
		CancellationToken cancellationToken)
		=> innerHandler.Handle(
			new SharedProcessBettingActionCommand(command.GameId, command.ActionType, command.Amount),
			cancellationToken);
}