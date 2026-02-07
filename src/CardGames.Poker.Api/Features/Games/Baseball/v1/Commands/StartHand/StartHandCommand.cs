using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.StartHand;

/// <summary>
/// Command to start a new hand in a Baseball game.
/// </summary>
public record StartHandCommand(Guid GameId)
	: IRequest<OneOf<StartHandSuccessful, StartHandError>>, IGameStateChangingCommand;
