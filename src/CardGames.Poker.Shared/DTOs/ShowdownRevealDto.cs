using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Represents the showdown reveal state for a single player.
/// </summary>
public record ShowdownRevealDto(
    /// <summary>
    /// The player's name/identifier.
    /// </summary>
    string PlayerName,

    /// <summary>
    /// The player's reveal status at showdown.
    /// </summary>
    ShowdownRevealStatus Status,

    /// <summary>
    /// The cards revealed by the player (null if mucked or pending).
    /// </summary>
    IReadOnlyList<CardDto>? RevealedCards,

    /// <summary>
    /// The evaluated hand if cards were revealed.
    /// </summary>
    HandDto? Hand,

    /// <summary>
    /// Whether this player was forced to reveal (e.g., all-in, last aggressor, winner).
    /// </summary>
    bool WasForcedReveal,

    /// <summary>
    /// The order in which this player revealed (1-based, null if not yet revealed).
    /// </summary>
    int? RevealOrder,

    /// <summary>
    /// Whether this player is eligible to win a pot.
    /// </summary>
    bool IsEligibleForPot);
