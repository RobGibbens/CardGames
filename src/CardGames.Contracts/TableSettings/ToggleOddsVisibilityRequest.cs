namespace CardGames.Poker.Api.Contracts;

/// <summary>
/// Request payload for toggling odds visibility for a table.
/// </summary>
public sealed record ToggleOddsVisibilityRequest
{
    /// <summary>
    /// Whether hand odds should be visible to all players.
    /// </summary>
    public required bool AreOddsVisibleToAllPlayers { get; init; }
}
