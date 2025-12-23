namespace CardGames.Contracts.SignalR;

/// <summary>
/// DTO broadcast to all players in a game when a new player joins.
/// Used to show toast notifications to existing players.
/// </summary>
public sealed record PlayerJoinedDto
{
    /// <summary>
    /// The unique identifier of the game.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// The name of the player who joined.
    /// </summary>
    public required string PlayerName { get; init; }

    /// <summary>
    /// The seat index the player joined at.
    /// </summary>
    public required int SeatIndex { get; init; }

    /// <summary>
    /// Whether the player can participate in the current hand.
    /// </summary>
    public required bool CanPlayCurrentHand { get; init; }

    /// <summary>
    /// A friendly message for toast notifications.
    /// </summary>
    public required string Message { get; init; }
}
