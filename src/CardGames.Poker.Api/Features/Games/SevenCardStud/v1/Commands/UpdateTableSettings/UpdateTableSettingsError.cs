namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.UpdateTableSettings;

/// <summary>
/// Error result when updating table settings fails.
/// </summary>
public record UpdateTableSettingsError
{
    /// <summary>
    /// The error code indicating the type of failure.
    /// </summary>
    public required UpdateTableSettingsErrorCode Code { get; init; }

    /// <summary>
    /// A human-readable description of the error.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The current phase of the game (if applicable).
    /// </summary>
    public string? CurrentPhase { get; init; }
}

/// <summary>
/// Error codes for UpdateTableSettings operation.
/// </summary>
public enum UpdateTableSettingsErrorCode
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
    /// The game is not in an editable phase.
    /// </summary>
    PhaseNotEditable,

    /// <summary>
    /// The concurrency token does not match (stale data).
    /// </summary>
    ConcurrencyConflict,

    /// <summary>
    /// Validation of the settings failed.
    /// </summary>
    ValidationFailed
}
