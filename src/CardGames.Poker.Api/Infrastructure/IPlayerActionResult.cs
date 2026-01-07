namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
/// Interface for command results that contain player action information
/// that should be broadcast via SignalR for display in the seat pill.
/// </summary>
public interface IPlayerActionResult
{
    /// <summary>
    /// Gets the unique identifier of the game.
    /// </summary>
    Guid GameId { get; }

    /// <summary>
    /// Gets the seat index of the player who performed the action.
    /// </summary>
    int PlayerSeatIndex { get; }

    /// <summary>
    /// Gets the name of the player who performed the action.
    /// </summary>
    string? PlayerName { get; }

    /// <summary>
    /// Gets the display description of the action (e.g., "Checked", "Raised 50", "Folded").
    /// </summary>
    string ActionDescription { get; }
}
