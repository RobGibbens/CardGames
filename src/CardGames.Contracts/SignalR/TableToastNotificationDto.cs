namespace CardGames.Contracts.SignalR;

/// <summary>
/// Notification sent to all players in a game to display a table toast.
/// </summary>
public sealed record TableToastNotificationDto
{
    /// <summary>
    /// The unique identifier of the game.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// The toast message to display.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The toast style (for example info, success, warning, or error).
    /// </summary>
    public string Type { get; init; } = "info";

    /// <summary>
    /// How long the toast should remain visible, in milliseconds.
    /// </summary>
    public int DurationMs { get; init; } = 4000;
}