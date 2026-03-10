namespace CardGames.Contracts.SignalR;

/// <summary>
/// DTO broadcast to game clients when odds visibility is toggled for a table.
/// </summary>
public sealed record OddsVisibilityUpdatedDto
{
    /// <summary>
    /// The unique identifier of the game/table.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// Whether odds are visible to all players at the table.
    /// </summary>
    public required bool AreOddsVisibleToAllPlayers { get; init; }

    /// <summary>
    /// The unique identifier of the user who made the update.
    /// </summary>
    public string? UpdatedById { get; init; }

    /// <summary>
    /// The name of the user who made the update.
    /// </summary>
    public string? UpdatedByName { get; init; }

    /// <summary>
    /// When the update was made.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
