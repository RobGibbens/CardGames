using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;
using SharedPerformShowdownError = CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownError;
using SharedPerformShowdownSuccessful = CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownSuccessful;

namespace CardGames.Poker.Api.Features.Games.PairPressure.v1.Commands.PerformShowdown;

public record PerformShowdownCommand(Guid GameId)
	: IRequest<OneOf<SharedPerformShowdownSuccessful, SharedPerformShowdownError>>, IGameStateChangingCommand;