using System.Collections.Frozen;
using CardGames.Poker.Games;
using CardGames.Poker.Games.FiveCardDraw;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.KingsAndLows;
using CardGames.Poker.Games.TwosJacksManWithTheAxe;

namespace CardGames.Poker.Api.Games;

/// <summary>
/// Registry for game rules metadata.
/// Provides access to game flow configuration and metadata for all supported game types.
/// </summary>
public static class PokerGameRulesRegistry
{
    private static readonly FrozenDictionary<string, Func<GameRules>> RulesByGameTypeCode =
        new Dictionary<string, Func<GameRules>>(StringComparer.OrdinalIgnoreCase)
        {
            [PokerGameMetadataRegistry.FiveCardDrawCode] = FiveCardDrawRules.CreateGameRules,
            [PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode] = TwosJacksManWithTheAxeRules.CreateGameRules,
            [PokerGameMetadataRegistry.KingsAndLowsCode] = KingsAndLowsRules.CreateGameRules,
            // Other games will be added as their rules are implemented
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Attempts to get game rules for the specified game type code.
    /// </summary>
    /// <param name="gameTypeCode">The game type code (e.g., "FIVECARDDRAW").</param>
    /// <param name="rules">The game rules if found.</param>
    /// <returns>True if rules were found; otherwise, false.</returns>
    public static bool TryGet(string? gameTypeCode, out GameRules? rules)
    {
        rules = null;
        
        if (string.IsNullOrWhiteSpace(gameTypeCode))
        {
            return false;
        }

        if (RulesByGameTypeCode.TryGetValue(gameTypeCode, out var factory))
        {
            try
            {
                rules = factory();
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets game rules for the specified game type code.
    /// </summary>
    /// <param name="gameTypeCode">The game type code (e.g., "FIVECARDDRAW").</param>
    /// <returns>The game rules.</returns>
    /// <exception cref="ArgumentException">Thrown when the game type code is unknown.</exception>
    public static GameRules Get(string gameTypeCode)
    {
        if (TryGet(gameTypeCode, out var rules) && rules is not null)
        {
            return rules;
        }

        throw new ArgumentException($"Unknown game type code: {gameTypeCode}", nameof(gameTypeCode));
    }

    /// <summary>
    /// Gets all available game type codes that have rules defined.
    /// </summary>
    public static IEnumerable<string> GetAvailableGameTypeCodes()
    {
        return RulesByGameTypeCode.Keys;
    }

    /// <summary>
    /// Checks if game rules are available for the specified game type code.
    /// </summary>
    public static bool IsAvailable(string? gameTypeCode)
    {
        return !string.IsNullOrWhiteSpace(gameTypeCode) && 
               RulesByGameTypeCode.ContainsKey(gameTypeCode);
    }
}
