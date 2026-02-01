namespace CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;

/// <summary>
/// Represents an error when starting a new hand.
/// </summary>
public record StartHandError
{
    /// <summary>
    /// The error message describing why the hand could not be started.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The error code for programmatic handling.
    /// </summary>
    public required StartHandErrorCode Code { get; init; }
}

/// <summary>
/// Error codes for start hand failures.
/// </summary>
public enum StartHandErrorCode
{
    /// <summary>
    /// The specified game was not found.
    /// </summary>
    GameNotFound,

    /// <summary>
    /// The game is not in a valid state to start a new hand.
    /// </summary>
    InvalidGameState,

    /// <summary>
    /// Not enough players with chips to continue.
    /// </summary>
    NotEnoughPlayers,

    /// <summary>
    /// The game type is not supported by the generic handler.
    /// </summary>
    UnsupportedGameType
}
