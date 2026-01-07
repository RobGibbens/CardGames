namespace CardGames.Contracts.SignalR;

/// <summary>
/// Notification sent when a player performs an action at the table.
/// Used to display the action temporarily in the player's seat pill.
/// </summary>
public sealed record PlayerActionPerformedDto
{
    /// <summary>
    /// The unique identifier of the game.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// The seat index of the player who performed the action.
    /// </summary>
    public required int SeatIndex { get; init; }

    /// <summary>
    /// The name of the player who performed the action.
    /// </summary>
    public string? PlayerName { get; init; }

    /// <summary>
    /// The display description of the action (e.g., "Checked", "Raised 50", "Folded", "Discarded 2").
    /// </summary>
    public required string ActionDescription { get; init; }

    /// <summary>
    /// The UTC timestamp when the action was performed.
    /// </summary>
    public DateTimeOffset PerformedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// How long in seconds the action should be displayed.
    /// </summary>
    public int DisplayDurationSeconds { get; init; } = 5;
}
