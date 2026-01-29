using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.JoinGame;

/// <summary>
/// Command to join a player to a specific seat in a game.
/// </summary>
/// <param name="GameId">The unique identifier of the game to join.</param>
/// <param name="SeatIndex">The zero-based seat index to occupy.</param>
/// <param name="StartingChips">The initial chip stack for the player.</param>
public record JoinGameCommand(Guid GameId, int SeatIndex, int StartingChips = 100)
    : IRequest<OneOf<JoinGameSuccessful, JoinGameError>>, IGameStateChangingCommand;  //TODO:ROB - Set this to 50, or better yet, prompt them first
