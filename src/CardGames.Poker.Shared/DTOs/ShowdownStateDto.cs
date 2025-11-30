using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Represents the complete state of a showdown in progress.
/// </summary>
public record ShowdownStateDto(
    /// <summary>
    /// Unique identifier for this showdown.
    /// </summary>
    Guid ShowdownId,

    /// <summary>
    /// The game identifier.
    /// </summary>
    Guid GameId,

    /// <summary>
    /// The hand number within the game.
    /// </summary>
    int HandNumber,

    /// <summary>
    /// The showdown order being used.
    /// </summary>
    ShowdownOrder ShowOrder,

    /// <summary>
    /// Whether mucking is allowed in this showdown.
    /// </summary>
    bool AllowMuck,

    /// <summary>
    /// Whether all players must show due to all-in action.
    /// </summary>
    bool ForceShowAllOnAllIn,

    /// <summary>
    /// Player who was the last aggressor (may be required to show first).
    /// </summary>
    string? LastAggressor,

    /// <summary>
    /// The current reveal states for all players eligible at showdown.
    /// </summary>
    IReadOnlyList<ShowdownRevealDto> PlayerReveals,

    /// <summary>
    /// The player who must act next at showdown (null if showdown is complete).
    /// </summary>
    string? NextToReveal,

    /// <summary>
    /// Whether there was all-in action during this hand.
    /// </summary>
    bool HadAllInAction,

    /// <summary>
    /// Whether the showdown is complete.
    /// </summary>
    bool IsComplete,

    /// <summary>
    /// Community cards revealed during showdown (for community card games).
    /// </summary>
    IReadOnlyList<CardDto>? CommunityCards,

    /// <summary>
    /// Timestamp when showdown started.
    /// </summary>
    DateTime StartedAt);
