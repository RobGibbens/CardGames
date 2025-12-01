namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Specifies the type of showdown animation.
/// </summary>
public enum ShowdownAnimationType
{
    /// <summary>Initial showdown reveal sequence.</summary>
    RevealSequence,

    /// <summary>Player revealing their cards.</summary>
    PlayerReveal,

    /// <summary>Player mucking their cards.</summary>
    PlayerMuck,

    /// <summary>Highlighting the winning hand.</summary>
    WinnerHighlight,

    /// <summary>Collecting pot to winner.</summary>
    PotAward,

    /// <summary>Running out remaining community cards (all-in scenario).</summary>
    RunOutBoard,

    /// <summary>Showing pot split among multiple winners.</summary>
    SplitPot
}

/// <summary>
/// Represents a single step in the showdown animation sequence.
/// </summary>
public record ShowdownAnimationStepDto(
    /// <summary>
    /// Type of animation to perform.
    /// </summary>
    ShowdownAnimationType AnimationType,

    /// <summary>
    /// Sequence number for ordering (0-based).
    /// </summary>
    int Sequence,

    /// <summary>
    /// Player name involved in this step (if applicable).
    /// </summary>
    string? PlayerName,

    /// <summary>
    /// Cards to reveal or highlight (if applicable).
    /// </summary>
    IReadOnlyList<CardDto>? Cards,

    /// <summary>
    /// The evaluated hand (if applicable).
    /// </summary>
    HandDto? Hand,

    /// <summary>
    /// Amount involved (for pot awards).
    /// </summary>
    int? Amount,

    /// <summary>
    /// Suggested duration for this animation step in milliseconds.
    /// </summary>
    int DurationMs,

    /// <summary>
    /// Additional message to display.
    /// </summary>
    string? Message);

/// <summary>
/// Represents a complete showdown animation sequence.
/// </summary>
public record ShowdownAnimationSequenceDto(
    /// <summary>
    /// Unique identifier for this animation sequence.
    /// </summary>
    Guid AnimationId,

    /// <summary>
    /// The showdown identifier.
    /// </summary>
    Guid ShowdownId,

    /// <summary>
    /// The game identifier.
    /// </summary>
    Guid GameId,

    /// <summary>
    /// The hand number.
    /// </summary>
    int HandNumber,

    /// <summary>
    /// Ordered list of animation steps to perform.
    /// </summary>
    IReadOnlyList<ShowdownAnimationStepDto> Steps,

    /// <summary>
    /// Total duration of the animation sequence in milliseconds.
    /// </summary>
    int TotalDurationMs,

    /// <summary>
    /// Whether this is an all-in showdown (board run-out included).
    /// </summary>
    bool IsAllInShowdown,

    /// <summary>
    /// Community cards (for reference during animation).
    /// </summary>
    IReadOnlyList<CardDto>? CommunityCards);

/// <summary>
/// Result of building a showdown animation.
/// </summary>
public record ShowdownAnimationBuildResult(
    /// <summary>
    /// Whether the animation was successfully built.
    /// </summary>
    bool Success,

    /// <summary>
    /// The animation sequence if successful.
    /// </summary>
    ShowdownAnimationSequenceDto? Animation,

    /// <summary>
    /// Error message if unsuccessful.
    /// </summary>
    string? ErrorMessage);
