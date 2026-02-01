namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Configuration for chip coverage checking behavior.
/// </summary>
/// <remarks>
/// Games like Kings and Lows require all players to be able to cover the pot
/// before a new hand can start. If a player cannot cover, the game pauses
/// for a configurable period to allow chip additions.
/// </remarks>
public sealed class ChipCheckConfiguration
{
    /// <summary>
    /// Gets whether chip coverage check is enabled.
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// Gets the duration to pause for players to add chips.
    /// </summary>
    public required TimeSpan PauseDuration { get; init; }

    /// <summary>
    /// Gets the action to take when a player cannot cover the pot after the pause expires.
    /// </summary>
    public required ChipShortageAction ShortageAction { get; init; }

    /// <summary>
    /// Default configuration for games without chip check requirements.
    /// </summary>
    public static ChipCheckConfiguration Disabled => new()
    {
        IsEnabled = false,
        PauseDuration = TimeSpan.Zero,
        ShortageAction = ChipShortageAction.None
    };

    /// <summary>
    /// Configuration for Kings and Lows style chip check.
    /// </summary>
    public static ChipCheckConfiguration KingsAndLowsDefault => new()
    {
        IsEnabled = true,
        PauseDuration = TimeSpan.FromMinutes(2),
        ShortageAction = ChipShortageAction.AutoDrop
    };
}

/// <summary>
/// Action to take when a player cannot cover the pot.
/// </summary>
public enum ChipShortageAction
{
    /// <summary>
    /// No action - chip check is disabled.
    /// </summary>
    None,

    /// <summary>
    /// Automatically drop (fold) the player on the next DropOrStay phase.
    /// </summary>
    AutoDrop,

    /// <summary>
    /// Sit the player out from the next hand.
    /// </summary>
    SitOut
}
