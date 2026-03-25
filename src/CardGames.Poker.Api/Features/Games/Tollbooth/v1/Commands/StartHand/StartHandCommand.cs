using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.StartHand;

public record StartHandCommand(Guid GameId) : IRequest<OneOf<StartHandSuccessful, StartHandError>>, IGameStateChangingCommand;
