namespace CardGames.Poker.Api.Features.Games.PhaseHandlers;

/// <summary>
/// Factory for creating phase-specific handlers.
/// </summary>
/// <remarks>
/// <para>
/// The factory resolves phase handlers based on the phase ID and optionally the game type.
/// This allows different games to have different implementations for the same phase,
/// or share a common implementation across multiple games.
/// </para>
/// <para>
/// Phase handlers are discovered via assembly scanning and registered by their
/// <see cref="IPhaseHandler.PhaseId"/> and <see cref="IPhaseHandler.ApplicableGameTypes"/>.
/// </para>
/// </remarks>
public interface IPhaseHandlerFactory
{
    /// <summary>
    /// Gets the phase handler for the specified phase and game type.
    /// </summary>
    /// <param name="phaseId">The phase ID (e.g., "DropOrStay", "PotMatching").</param>
    /// <param name="gameTypeCode">The game type code (e.g., "KINGSANDLOWS").</param>
    /// <returns>The appropriate phase handler.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no handler is found for the specified phase and game type.
    /// </exception>
    IPhaseHandler GetHandler(string phaseId, string gameTypeCode);

    /// <summary>
    /// Attempts to get a handler for the specified phase and game type.
    /// </summary>
    /// <param name="phaseId">The phase ID.</param>
    /// <param name="gameTypeCode">The game type code.</param>
    /// <param name="handler">The handler if found; otherwise, null.</param>
    /// <returns>True if a handler was found; otherwise, false.</returns>
    bool TryGetHandler(string phaseId, string gameTypeCode, out IPhaseHandler? handler);

    /// <summary>
    /// Gets all registered phase handlers.
    /// </summary>
    /// <returns>A read-only collection of all registered handlers.</returns>
    IReadOnlyCollection<IPhaseHandler> GetAllHandlers();

    /// <summary>
    /// Gets all handlers that apply to the specified game type.
    /// </summary>
    /// <param name="gameTypeCode">The game type code.</param>
    /// <returns>A read-only collection of applicable handlers.</returns>
    IReadOnlyCollection<IPhaseHandler> GetHandlersForGameType(string gameTypeCode);
}
