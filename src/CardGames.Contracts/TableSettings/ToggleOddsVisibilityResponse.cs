namespace CardGames.Poker.Api.Contracts;

/// <summary>
/// Response payload for odds visibility toggle.
/// </summary>
public sealed record ToggleOddsVisibilityResponse
{
    /// <summary>
    /// The game identifier.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// Whether odds are visible to all players.
    /// </summary>
    public required bool AreOddsVisibleToAllPlayers { get; init; }
}
