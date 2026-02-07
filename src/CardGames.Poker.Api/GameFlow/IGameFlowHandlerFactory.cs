namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Factory for creating game-specific flow handlers.
/// Uses the game type code to resolve the appropriate handler.
/// </summary>
/// <remarks>
/// <para>
/// This factory follows the same pattern as <see cref="Poker.Evaluation.IHandEvaluatorFactory"/>
/// to provide consistent extensibility across the codebase.
/// </para>
/// <para>
/// Implementations should use assembly scanning or DI to discover all
/// <see cref="IGameFlowHandler"/> implementations automatically.
/// </para>
/// </remarks>
public interface IGameFlowHandlerFactory
{
    /// <summary>
    /// Gets the flow handler for the specified game type.
    /// </summary>
    /// <param name="gameTypeCode">The game type code (e.g., "FIVECARDDRAW").</param>
    /// <returns>
    /// The appropriate <see cref="IGameFlowHandler"/> for the game type.
    /// Returns a default handler for unknown game types.
    /// </returns>
    IGameFlowHandler GetHandler(string? gameTypeCode);

    /// <summary>
    /// Attempts to get a handler for the specified game type.
    /// </summary>
    /// <param name="gameTypeCode">The game type code.</param>
    /// <param name="handler">The handler if found.</param>
    /// <returns>True if a specific handler was found; false if the default was used.</returns>
    bool TryGetHandler(string? gameTypeCode, out IGameFlowHandler? handler);
}
