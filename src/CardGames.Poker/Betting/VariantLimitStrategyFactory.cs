using System;
using CardGames.Poker.Shared.DTOs.RuleSets;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.RuleSets;
using SharedLimitType = CardGames.Poker.Shared.Enums.LimitType;

namespace CardGames.Poker.Betting;

/// <summary>
/// Factory for creating limit strategies based on variant configuration.
/// Provides integration between RuleSetDto limit types and betting strategies.
/// </summary>
public static class VariantLimitStrategyFactory
{
    /// <summary>
    /// Creates a limit strategy from a VariantConfigLoader.
    /// </summary>
    /// <param name="config">The variant configuration.</param>
    /// <param name="smallBet">The small bet amount (for fixed limit games).</param>
    /// <param name="bigBet">The big bet amount (for fixed limit games).</param>
    /// <param name="bettingRoundOrder">The current betting round order (for fixed limit bet sizing).</param>
    /// <returns>The appropriate limit strategy.</returns>
    public static ILimitStrategy CreateFromConfig(
        VariantConfigLoader config,
        int smallBet = 0,
        int bigBet = 0,
        int bettingRoundOrder = 0)
    {
        ArgumentNullException.ThrowIfNull(config);

        return CreateFromLimitType(
            config.LimitType,
            smallBet,
            bigBet,
            config.UsesBigBet(bettingRoundOrder));
    }

    /// <summary>
    /// Creates a limit strategy from a RuleSetDto.
    /// </summary>
    /// <param name="ruleSet">The ruleset containing limit type configuration.</param>
    /// <param name="smallBet">The small bet amount (for fixed limit games).</param>
    /// <param name="bigBet">The big bet amount (for fixed limit games).</param>
    /// <param name="bettingRoundOrder">The current betting round order (for fixed limit bet sizing).</param>
    /// <returns>The appropriate limit strategy.</returns>
    public static ILimitStrategy CreateFromRuleSet(
        RuleSetDto ruleSet,
        int smallBet = 0,
        int bigBet = 0,
        int bettingRoundOrder = 0)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);

        var config = VariantConfigLoader.FromRuleSet(ruleSet);
        return CreateFromConfig(config, smallBet, bigBet, bettingRoundOrder);
    }

    /// <summary>
    /// Creates a limit strategy from a LimitType enum value (from Shared).
    /// </summary>
    /// <param name="limitType">The limit type.</param>
    /// <param name="smallBet">The small bet amount (for fixed limit games).</param>
    /// <param name="bigBet">The big bet amount (for fixed limit games).</param>
    /// <param name="useBigBet">Whether to use the big bet (for fixed limit late streets).</param>
    /// <returns>The appropriate limit strategy.</returns>
    public static ILimitStrategy CreateFromLimitType(
        SharedLimitType limitType,
        int smallBet = 0,
        int bigBet = 0,
        bool useBigBet = false)
    {
        return limitType switch
        {
            SharedLimitType.NoLimit => new NoLimitStrategy(),
            SharedLimitType.PotLimit => new PotLimitStrategy(),
            SharedLimitType.FixedLimit => new FixedLimitStrategy(smallBet, bigBet, useBigBet),
            SharedLimitType.SpreadLimit => new NoLimitStrategy(), // Fallback to NoLimit for SpreadLimit
            _ => new NoLimitStrategy()
        };
    }

    /// <summary>
    /// Creates a limit strategy for a specific betting round based on the ruleset.
    /// </summary>
    /// <param name="ruleSet">The ruleset.</param>
    /// <param name="bettingRoundOrder">The 0-based betting round order.</param>
    /// <param name="bigBlind">The big blind amount (used for bet sizing).</param>
    /// <returns>The appropriate limit strategy for the betting round.</returns>
    public static ILimitStrategy CreateForBettingRound(
        RuleSetDto ruleSet,
        int bettingRoundOrder,
        int bigBlind)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);

        var config = VariantConfigLoader.FromRuleSet(ruleSet);

        // For fixed limit games, calculate small bet and big bet from big blind
        int smallBet = bigBlind;
        int bigBet = bigBlind * 2;

        return CreateFromConfig(config, smallBet, bigBet, bettingRoundOrder);
    }

    /// <summary>
    /// Gets the display name for a limit type.
    /// </summary>
    /// <param name="limitType">The limit type.</param>
    /// <returns>A human-readable name for the limit type.</returns>
    public static string GetLimitTypeDisplayName(SharedLimitType limitType)
    {
        return limitType switch
        {
            SharedLimitType.NoLimit => "No Limit",
            SharedLimitType.PotLimit => "Pot Limit",
            SharedLimitType.FixedLimit => "Fixed Limit",
            SharedLimitType.SpreadLimit => "Spread Limit",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Gets an abbreviated display name for a limit type.
    /// </summary>
    /// <param name="limitType">The limit type.</param>
    /// <returns>An abbreviated name for the limit type.</returns>
    public static string GetLimitTypeAbbreviation(SharedLimitType limitType)
    {
        return limitType switch
        {
            SharedLimitType.NoLimit => "NL",
            SharedLimitType.PotLimit => "PL",
            SharedLimitType.FixedLimit => "FL",
            SharedLimitType.SpreadLimit => "SL",
            _ => ""
        };
    }
}
