using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleOddsVisibility;

/// <summary>
/// Command to toggle odds visibility for a game.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="AreOddsVisibleToAllPlayers">Whether odds should be visible to all players.</param>
public sealed record ToggleOddsVisibilityCommand(
    Guid GameId,
    bool AreOddsVisibleToAllPlayers)
    : IRequest<OneOf<ToggleOddsVisibilitySuccessful, ToggleOddsVisibilityError>>, IGameStateChangingCommand;
