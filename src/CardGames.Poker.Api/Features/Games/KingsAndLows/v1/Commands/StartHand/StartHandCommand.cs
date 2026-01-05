using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.StartHand;

/// <summary>
/// Command to start a new hand in a Kings and Lows game.
/// </summary>
/// <param name="GameId">The unique identifier of the game to start a new hand in.</param>
public record StartHandCommand(Guid GameId) : IRequest<OneOf<StartHandSuccessful, StartHandError>>, IGameStateChangingCommand;
