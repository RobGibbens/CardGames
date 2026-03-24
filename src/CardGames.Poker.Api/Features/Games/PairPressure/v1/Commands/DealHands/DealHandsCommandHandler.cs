using MediatR;
using OneOf;
using SharedDealHandsCommand = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands.DealHandsCommand;
using SharedDealHandsError = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands.DealHandsError;
using SharedDealHandsSuccessful = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands.DealHandsSuccessful;

namespace CardGames.Poker.Api.Features.Games.PairPressure.v1.Commands.DealHands;

public sealed class DealHandsCommandHandler(
	IRequestHandler<SharedDealHandsCommand, OneOf<SharedDealHandsSuccessful, SharedDealHandsError>> innerHandler)
	: IRequestHandler<DealHandsCommand, OneOf<SharedDealHandsSuccessful, SharedDealHandsError>>
{
	public Task<OneOf<SharedDealHandsSuccessful, SharedDealHandsError>> Handle(
		DealHandsCommand command,
		CancellationToken cancellationToken)
		=> innerHandler.Handle(new SharedDealHandsCommand(command.GameId), cancellationToken);
}