using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// Represents configuration options for creating a poker table.
/// </summary>
public record TableConfigDto(
    /// <summary>
    /// The poker variant to be played at the table.
    /// </summary>
    PokerVariant Variant,

    /// <summary>
    /// Maximum number of seats at the table (2-10).
    /// </summary>
    int MaxSeats,

    /// <summary>
    /// Small blind amount.
    /// </summary>
    int SmallBlind,

    /// <summary>
    /// Big blind amount.
    /// </summary>
    int BigBlind,

    /// <summary>
    /// Betting limit type for the table.
    /// </summary>
    LimitType LimitType = LimitType.NoLimit,

    /// <summary>
    /// Minimum buy-in amount.
    /// </summary>
    int MinBuyIn = 0,

    /// <summary>
    /// Maximum buy-in amount.
    /// </summary>
    int MaxBuyIn = 0,

    /// <summary>
    /// Ante amount (optional, defaults to 0).
    /// </summary>
    int Ante = 0);
