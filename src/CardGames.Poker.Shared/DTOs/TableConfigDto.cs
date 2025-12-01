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
    int Ante = 0,

    /// <summary>
    /// Timer configuration for the table (optional).
    /// </summary>
    TimerConfigDto? TimerConfig = null);

/// <summary>
/// Represents timer configuration settings for a poker table.
/// </summary>
public record TimerConfigDto(
    /// <summary>
    /// Turn timeout duration in seconds. Set to 0 to disable timers.
    /// </summary>
    int TurnTimeoutSeconds = 30,

    /// <summary>
    /// Warning threshold in seconds before timeout.
    /// </summary>
    int WarningThresholdSeconds = 10,

    /// <summary>
    /// Time bank duration in seconds per player.
    /// </summary>
    int TimeBankSeconds = 60,

    /// <summary>
    /// Whether to automatically perform a default action when the timer expires.
    /// </summary>
    bool AutoActOnTimeout = true)
{
    /// <summary>
    /// Default timer configuration for cash games.
    /// </summary>
    public static TimerConfigDto CashGame { get; } = new(
        TurnTimeoutSeconds: 30,
        WarningThresholdSeconds: 10,
        TimeBankSeconds: 60,
        AutoActOnTimeout: true);

    /// <summary>
    /// Default timer configuration for tournaments.
    /// </summary>
    public static TimerConfigDto Tournament { get; } = new(
        TurnTimeoutSeconds: 15,
        WarningThresholdSeconds: 5,
        TimeBankSeconds: 30,
        AutoActOnTimeout: true);

    /// <summary>
    /// Disabled timer configuration.
    /// </summary>
    public static TimerConfigDto Disabled { get; } = new(
        TurnTimeoutSeconds: 0,
        WarningThresholdSeconds: 0,
        TimeBankSeconds: 0,
        AutoActOnTimeout: false);
}
