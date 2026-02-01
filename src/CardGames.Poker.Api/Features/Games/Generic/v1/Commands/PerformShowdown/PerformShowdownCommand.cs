using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;

/// <summary>
/// Generic command to perform the showdown phase and determine the winner(s) in any poker game.
/// The game type is determined from the game entity and routed to the appropriate
/// hand evaluator via <see cref="Poker.Evaluation.IHandEvaluatorFactory"/>.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <remarks>
/// This generic command replaces the game-specific PerformShowdownCommand implementations
/// (FiveCardDraw, SevenCardStud, KingsAndLows, etc.) by using the factory pattern
/// to get the appropriate hand evaluator for each game type.
/// </remarks>
public record PerformShowdownCommand(Guid GameId)
    : IRequest<OneOf<PerformShowdownSuccessful, PerformShowdownError>>, IGameStateChangingCommand;
