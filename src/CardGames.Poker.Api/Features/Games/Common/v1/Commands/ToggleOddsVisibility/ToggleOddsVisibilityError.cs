namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleOddsVisibility;

/// <summary>
/// Error result when toggling odds visibility fails.
/// </summary>
public sealed record ToggleOddsVisibilityError
{
    /// <summary>
    /// The error code indicating the type of failure.
    /// </summary>
    public required ToggleOddsVisibilityErrorCode Code { get; init; }

    /// <summary>
    /// A human-readable description of the error.
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Error codes for ToggleOddsVisibility operation.
/// </summary>
public enum ToggleOddsVisibilityErrorCode
{
    /// <summary>
    /// The specified game was not found.
    /// </summary>
    GameNotFound,

    /// <summary>
    /// The user is not authorized to edit this table.
    /// </summary>
    NotAuthorized,

    /// <summary>
    /// The game has ended and cannot be changed.
    /// </summary>
    GameEnded
}
