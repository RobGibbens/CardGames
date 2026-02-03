using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CardGames.Poker.Evaluation.Evaluators;

namespace CardGames.Poker.Evaluation;

/// <summary>
/// Factory for creating game-specific hand evaluators.
/// Maps game type codes to the appropriate <see cref="IHandEvaluator"/> implementation.
/// </summary>
public sealed class HandEvaluatorFactory : IHandEvaluatorFactory
{
    /// <summary>
    /// Game type code for Five Card Draw.
    /// </summary>
    public const string FiveCardDrawCode = "FIVECARDDRAW";

    /// <summary>
    /// Game type code for Twos, Jacks, Man with the Axe.
    /// </summary>
    public const string TwosJacksManWithTheAxeCode = "TWOSJACKSMANWITHTHEAXE";

    /// <summary>
    /// Game type code for Kings and Lows.
    /// </summary>
    public const string KingsAndLowsCode = "KINGSANDLOWS";

    /// <summary>
    /// Game type code for Seven Card Stud.
    /// </summary>
    public const string SevenCardStudCode = "SEVENCARDSTUD";

    private static readonly DrawHandEvaluator DefaultEvaluator = new();

    private static readonly FrozenDictionary<string, IHandEvaluator> EvaluatorsByGameCode;

    static HandEvaluatorFactory()
    {
        var evaluators = new Dictionary<string, IHandEvaluator>(StringComparer.OrdinalIgnoreCase);

        var assembly = typeof(IHandEvaluator).Assembly;
        var evaluatorTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IHandEvaluator).IsAssignableFrom(t));

        foreach (var type in evaluatorTypes)
        {
            var attributes = type.GetCustomAttributes<HandEvaluatorAttribute>();
            foreach (var attribute in attributes)
            {
                // Only create instance if not already created for this type to save resources? 
                // But we need one instance per code probably or one instance shared?
                // Shared instance for multiple codes if same type?
                // For simplicity, create new instance. They are stateless mostly.
                if (Activator.CreateInstance(type) is IHandEvaluator instance)
                {
                    evaluators[attribute.GameTypeCode] = instance;
                }
            }
        }

        EvaluatorsByGameCode = evaluators.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IHandEvaluator GetEvaluator(string? gameTypeCode)
    {
        if (string.IsNullOrWhiteSpace(gameTypeCode))
        {
            return DefaultEvaluator;
        }

        return EvaluatorsByGameCode.TryGetValue(gameTypeCode, out var evaluator)
            ? evaluator
            : DefaultEvaluator;
    }

    /// <inheritdoc />
    public bool TryGetEvaluator(string? gameTypeCode, out IHandEvaluator evaluator)
    {
        if (string.IsNullOrWhiteSpace(gameTypeCode))
        {
            evaluator = DefaultEvaluator;
            return false;
        }

        if (EvaluatorsByGameCode.TryGetValue(gameTypeCode, out var found))
        {
            evaluator = found;
            return true;
        }

        evaluator = DefaultEvaluator;
        return false;
    }

    /// <summary>
    /// Gets whether a specific evaluator exists for the given game type code.
    /// </summary>
    /// <param name="gameTypeCode">The game type code to check.</param>
    /// <returns>True if a specific evaluator is registered; otherwise, false.</returns>
    public static bool HasEvaluator(string? gameTypeCode)
    {
        return !string.IsNullOrWhiteSpace(gameTypeCode) &&
               EvaluatorsByGameCode.ContainsKey(gameTypeCode);
    }

    /// <summary>
    /// Gets all registered game type codes.
    /// </summary>
    /// <returns>A collection of registered game type codes.</returns>
    public static IEnumerable<string> GetRegisteredGameCodes()
    {
        return EvaluatorsByGameCode.Keys;
    }
}
