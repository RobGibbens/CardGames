using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.PerformShowdown;

/// <summary>
/// Command to perform the showdown phase and determine the winner(s) in a Kings and Lows game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
public record PerformShowdownCommand(Guid GameId)
	: IRequest<OneOf<PerformShowdownSuccessful, PerformShowdownError>>, IGameStateChangingCommand;
