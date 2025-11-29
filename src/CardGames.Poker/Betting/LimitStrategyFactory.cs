namespace CardGames.Poker.Betting;

/// <summary>
/// Represents the type of betting limit for a poker game.
/// </summary>
public enum LimitType
{
    /// <summary>No limit on bet sizes.</summary>
    NoLimit,

    /// <summary>Bets limited to the current pot size.</summary>
    PotLimit,

    /// <summary>Fixed bet sizes based on the round.</summary>
    FixedLimit
}

/// <summary>
/// Factory for creating limit strategies based on limit type.
/// </summary>
public static class LimitStrategyFactory
{
    /// <summary>
    /// Creates a limit strategy based on the specified limit type.
    /// </summary>
    /// <param name="limitType">The type of betting limit.</param>
    /// <param name="smallBet">The small bet amount (used for fixed limit).</param>
    /// <param name="bigBet">The big bet amount (used for fixed limit).</param>
    /// <param name="useBigBet">Whether to use the big bet (for later streets in fixed limit).</param>
    /// <returns>The appropriate limit strategy.</returns>
    public static ILimitStrategy Create(LimitType limitType, int smallBet = 0, int bigBet = 0, bool useBigBet = false)
    {
        return limitType switch
        {
            LimitType.NoLimit => new NoLimitStrategy(),
            LimitType.PotLimit => new PotLimitStrategy(),
            LimitType.FixedLimit => new FixedLimitStrategy(smallBet, bigBet, useBigBet),
            _ => new NoLimitStrategy()
        };
    }

    /// <summary>
    /// Creates a No Limit strategy.
    /// </summary>
    public static ILimitStrategy CreateNoLimit() => new NoLimitStrategy();

    /// <summary>
    /// Creates a Pot Limit strategy.
    /// </summary>
    public static ILimitStrategy CreatePotLimit() => new PotLimitStrategy();

    /// <summary>
    /// Creates a Fixed Limit strategy.
    /// </summary>
    /// <param name="smallBet">The small bet amount.</param>
    /// <param name="bigBet">The big bet amount.</param>
    /// <param name="useBigBet">Whether to use the big bet (for turn/river).</param>
    public static ILimitStrategy CreateFixedLimit(int smallBet, int bigBet, bool useBigBet = false) 
        => new FixedLimitStrategy(smallBet, bigBet, useBigBet);
}
