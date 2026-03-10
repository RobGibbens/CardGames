namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleOddsVisibility;

/// <summary>
/// Successful result for toggling odds visibility.
/// </summary>
public sealed record ToggleOddsVisibilitySuccessful
{
    /// <summary>
    /// The unique identifier of the game/table.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// Whether odds are visible to all players.
    /// </summary>
    public required bool AreOddsVisibleToAllPlayers { get; init; }
}
