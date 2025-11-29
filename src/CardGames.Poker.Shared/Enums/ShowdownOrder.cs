namespace CardGames.Poker.Shared.Enums;

/// <summary>
/// Represents the order in which hands are shown at showdown.
/// </summary>
public enum ShowdownOrder
{
    /// <summary>
    /// Last aggressor shows first.
    /// </summary>
    LastAggressor,

    /// <summary>
    /// Clockwise from the button.
    /// </summary>
    ClockwiseFromButton,

    /// <summary>
    /// Counter-clockwise from the button.
    /// </summary>
    CounterClockwiseFromButton,

    /// <summary>
    /// All players show simultaneously.
    /// </summary>
    Simultaneous
}
