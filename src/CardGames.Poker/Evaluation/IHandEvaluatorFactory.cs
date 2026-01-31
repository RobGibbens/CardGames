namespace CardGames.Poker.Evaluation;

/// <summary>
/// Factory interface for creating game-specific hand evaluators.
/// Use this to get the appropriate <see cref="IHandEvaluator"/> for a poker variant.
/// </summary>
/// <remarks>
/// This factory allows the API and service layers to evaluate hands without
/// hardcoding game type checks. New game types can be added by implementing
/// <see cref="IHandEvaluator"/> and registering with the factory.
/// </remarks>
public interface IHandEvaluatorFactory
{
    /// <summary>
    /// Gets a hand evaluator for the specified game type code.
    /// </summary>
    /// <param name="gameTypeCode">The game type code (e.g., "FIVECARDDRAW", "SEVENCARDSTUD").</param>
    /// <returns>
    /// An <see cref="IHandEvaluator"/> appropriate for the game type.
    /// Returns a default draw evaluator for unknown game types.
    /// </returns>
    IHandEvaluator GetEvaluator(string? gameTypeCode);

    /// <summary>
    /// Attempts to get a hand evaluator for the specified game type code.
    /// </summary>
    /// <param name="gameTypeCode">The game type code.</param>
    /// <param name="evaluator">The evaluator if found.</param>
    /// <returns>True if a specific evaluator was found; false if the default was used.</returns>
    bool TryGetEvaluator(string? gameTypeCode, out IHandEvaluator evaluator);
}
