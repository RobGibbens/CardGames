namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Represents a single winner in the showdown with their hand and pot share.
/// </summary>
public record WinnerInfoDto(
    /// <summary>
    /// The winner's name/identifier.
    /// </summary>
    string PlayerName,

    /// <summary>
    /// The winning hand evaluated (null if won by fold).
    /// </summary>
    HandDto? Hand,

    /// <summary>
    /// The cards that make up the winning hand (5 cards). Null if won by fold.
    /// </summary>
    IReadOnlyList<CardDto>? WinningCards,

    /// <summary>
    /// The hole cards of the winner.
    /// </summary>
    IReadOnlyList<CardDto>? HoleCards,

    /// <summary>
    /// The amount won from the pot.
    /// </summary>
    int AmountWon,

    /// <summary>
    /// Whether this winner tied with others for the pot.
    /// </summary>
    bool IsTie,

    /// <summary>
    /// The pot index this winner claimed (0 for main pot, 1+ for side pots).
    /// </summary>
    int PotIndex);

/// <summary>
/// Represents a complete winner announcement for a showdown.
/// </summary>
public record WinnerAnnouncementDto(
    /// <summary>
    /// Unique identifier for the showdown.
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
    /// List of all winners with their details.
    /// </summary>
    IReadOnlyList<WinnerInfoDto> Winners,

    /// <summary>
    /// Total pot amount distributed.
    /// </summary>
    int TotalPotDistributed,

    /// <summary>
    /// Whether there were multiple winners (split pot).
    /// </summary>
    bool IsSplitPot,

    /// <summary>
    /// Whether the hand was won by everyone folding (no showdown).
    /// </summary>
    bool WonByFold,

    /// <summary>
    /// Human-readable summary of the result.
    /// </summary>
    string Summary);
