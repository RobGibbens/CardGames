using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;

/// <summary>
/// Generic command to start a new hand in any poker game.
/// The game type is determined from the game entity and routed to the appropriate
/// <see cref="GameFlow.IGameFlowHandler"/> implementation.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <remarks>
/// This generic command replaces the game-specific StartHandCommand implementations
/// (FiveCardDraw, SevenCardStud, KingsAndLows, etc.) by using the strategy pattern
/// to encapsulate game-specific logic in flow handlers.
/// </remarks>
public record StartHandCommand(Guid GameId)
    : IRequest<OneOf<StartHandSuccessful, StartHandError>>, IGameStateChangingCommand;
