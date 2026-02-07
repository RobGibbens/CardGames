using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.PerformShowdown;

public record PerformShowdownCommand(Guid GameId)
	: IRequest<OneOf<PerformShowdownSuccessful, PerformShowdownError>>, IGameStateChangingCommand;
