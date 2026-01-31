using System;
using System.Collections.Frozen;
using System.Collections.Generic;
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

    private static readonly FrozenDictionary<string, IHandEvaluator> EvaluatorsByGameCode =
        new Dictionary<string, IHandEvaluator>(StringComparer.OrdinalIgnoreCase)
        {
            [FiveCardDrawCode] = new DrawHandEvaluator(),
            [TwosJacksManWithTheAxeCode] = new TwosJacksManWithTheAxeHandEvaluator(),
            [KingsAndLowsCode] = new KingsAndLowsHandEvaluator(),
            [SevenCardStudCode] = new SevenCardStudHandEvaluator(),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

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
