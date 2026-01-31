using System.Collections.Frozen;
using System.Reflection;
using CardGames.Poker.Games;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Api.Games;

/// <summary>
/// Registry for game rules metadata. Uses assembly scanning to discover all
/// <see cref="IPokerGame"/> implementations and caches their <see cref="GameRules"/>.
/// </summary>
/// <remarks>
/// New game types are automatically discovered when they implement <see cref="IPokerGame"/>
/// and are decorated with <see cref="PokerGameMetadataAttribute"/>. The <see cref="IPokerGame.GetGameRules"/>
/// method is called to obtain the rules for each game type. No manual registration required.
/// </remarks>
public static class PokerGameRulesRegistry
{
    private static readonly FrozenDictionary<string, GameRules> RulesByGameTypeCode;

    static PokerGameRulesRegistry()
    {
        var rulesDict = new Dictionary<string, GameRules>(StringComparer.OrdinalIgnoreCase);

        var pokerGameInterface = typeof(IPokerGame);
        var assembly = pokerGameInterface.Assembly;

        var gameTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && pokerGameInterface.IsAssignableFrom(t));

        foreach (var gameType in gameTypes)
        {
            var attribute = gameType.GetCustomAttribute<PokerGameMetadataAttribute>(inherit: false);
            if (attribute is null)
            {
                continue;
            }

            try
            {
                var instance = CreateGameInstance(gameType);
                if (instance is not null)
                {
                    var rules = instance.GetGameRules();
                    rulesDict[attribute.Code] = rules;
                }
            }
            catch
            {
                // Skip games that can't be instantiated for rules discovery.
                // They may have required constructor parameters.
            }
        }

        RulesByGameTypeCode = rulesDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates an instance of the game type for rules discovery.
    /// Tries parameterless constructor first, then attempts to provide default values.
    /// </summary>
    private static IPokerGame? CreateGameInstance(Type gameType)
    {
        // Try parameterless constructor first
        var parameterlessCtor = gameType.GetConstructor(Type.EmptyTypes);
        if (parameterlessCtor is not null)
        {
            return Activator.CreateInstance(gameType) as IPokerGame;
        }

        // Try to find a constructor and provide default values
        var constructors = gameType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (constructors.Length == 0)
        {
            return null;
        }

        var ctor = constructors[0];
        var parameters = ctor.GetParameters();
        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            args[i] = paramType.IsValueType ? Activator.CreateInstance(paramType) : null;
        }

        return ctor.Invoke(args) as IPokerGame;
    }

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

        return RulesByGameTypeCode.TryGetValue(gameTypeCode, out rules);
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
    /// Gets all registered game rules.
    /// </summary>
    public static IEnumerable<GameRules> GetAllRules()
    {
        return RulesByGameTypeCode.Values;
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
