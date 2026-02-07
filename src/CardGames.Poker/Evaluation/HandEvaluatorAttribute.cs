using System;

namespace CardGames.Poker.Evaluation;

/// <summary>
/// Specifies the game type code(s) that a hand evaluator supports.
/// </summary>
/// <param name="gameTypeCode">The game type code.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class HandEvaluatorAttribute(string gameTypeCode) : Attribute
{
    /// <summary>
    /// Gets the game type code.
    /// </summary>
    public string GameTypeCode { get; } = gameTypeCode;
}
