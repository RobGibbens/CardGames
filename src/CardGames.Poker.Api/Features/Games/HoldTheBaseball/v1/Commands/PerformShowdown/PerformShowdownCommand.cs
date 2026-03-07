using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1.Commands.PerformShowdown;

/// <summary>
/// Command to perform the showdown phase for a Hold the Baseball game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
public record PerformShowdownCommand(Guid GameId)
	: IRequest<OneOf<PerformShowdownSuccessful, PerformShowdownError>>, IGameStateChangingCommand;
