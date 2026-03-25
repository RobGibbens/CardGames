using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;
using SharedDealHandsError = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands.DealHandsError;
using SharedDealHandsSuccessful = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands.DealHandsSuccessful;

namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.DealHands;

public record DealHandsCommand(Guid GameId)
	: IRequest<OneOf<SharedDealHandsSuccessful, SharedDealHandsError>>, IGameStateChangingCommand;
