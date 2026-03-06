using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.HoldEm.v1.Commands.StartHand;

/// <summary>
/// Command to start a new hand in a Texas Hold 'Em game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
public record StartHandCommand(Guid GameId)
	: IRequest<OneOf<StartHandSuccessful, StartHandError>>, IGameStateChangingCommand;
