namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.JoinGame;

/// <summary>
/// Error response for a failed join game operation.
/// </summary>
/// <param name="Code">The error code.</param>
/// <param name="Message">A human-readable error message.</param>
public record JoinGameError(JoinGameErrorCode Code, string Message);

/// <summary>
/// Error codes for join game failures.
/// </summary>
public enum JoinGameErrorCode
{
    /// <summary>
    /// The specified game was not found.
    /// </summary>
    GameNotFound,

    /// <summary>
    /// The specified seat is already occupied.
    /// </summary>
    SeatOccupied,

    /// <summary>
    /// The player is already seated at another position in this game.
    /// </summary>
    AlreadySeated,

    /// <summary>
    /// The game has reached its maximum number of players.
    /// </summary>
    MaxPlayersReached,

    /// <summary>
    /// The specified seat index is invalid.
    /// </summary>
    InvalidSeatIndex,

    /// <summary>
    /// The game has ended and is no longer accepting players.
    /// </summary>
    GameEnded
}
