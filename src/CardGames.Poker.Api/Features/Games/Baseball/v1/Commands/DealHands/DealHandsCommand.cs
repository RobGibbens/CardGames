using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.DealHands;

public record DealHandsCommand(Guid GameId)
	: IRequest<OneOf<DealHandsSuccessful, DealHandsError>>, IGameStateChangingCommand;
