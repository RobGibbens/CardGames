namespace CardGames.Poker.Shared.Enums;

/// <summary>
/// Represents the betting limit type for a poker game.
/// </summary>
public enum LimitType
{
    /// <summary>
    /// No limit on bet sizes.
    /// </summary>
    NoLimit,

    /// <summary>
    /// Fixed bet sizes based on the round.
    /// </summary>
    FixedLimit,

    /// <summary>
    /// Bets limited to the current pot size.
    /// </summary>
    PotLimit,

    /// <summary>
    /// Spread limit with min/max range.
    /// </summary>
    SpreadLimit
}
