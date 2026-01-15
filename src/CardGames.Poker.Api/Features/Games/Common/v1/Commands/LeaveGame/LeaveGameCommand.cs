using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.LeaveGame;

/// <summary>
/// Command to leave a game table.
/// </summary>
/// <param name="GameId">The unique identifier of the game to leave.</param>
public sealed record LeaveGameCommand(Guid GameId)
	: IRequest<OneOf<LeaveGameSuccessful, LeaveGameError>>,
	  IGameStateChangingCommand;
