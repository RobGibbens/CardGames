using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.DeleteGame;

/// <summary>
/// Command to soft delete a game.
/// </summary>
/// <param name="GameId">The unique identifier of the game to delete.</param>
public record DeleteGameCommand(Guid GameId) : IRequest<OneOf<DeleteGameSuccessful, DeleteGameError>>;
