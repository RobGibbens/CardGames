using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.PairPressure.v1.Commands.CollectAntes;

public record CollectAntesCommand(Guid GameId) : IRequest<OneOf<CollectAntesSuccessful, CollectAntesError>>, IGameStateChangingCommand;