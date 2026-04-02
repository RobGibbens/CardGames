namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Manages the lifecycle of in-memory game state instances.
/// Singleton holding a <c>ConcurrentDictionary</c> of all active game states.
/// </summary>
public interface IGameStateManager
{
    /// <summary>
    /// Tries to get an existing in-memory game state.
    /// </summary>
    bool TryGetGame(Guid gameId, out ActiveGameRuntimeState state);

    /// <summary>
    /// Gets an existing game state or loads it from the database via the hydrator.
    /// </summary>
    Task<ActiveGameRuntimeState?> GetOrLoadGameAsync(Guid gameId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a game state (e.g., after initial creation or hydration).
    /// </summary>
    void SetGame(ActiveGameRuntimeState state);

    /// <summary>
    /// Removes a game from the in-memory store.
    /// </summary>
    bool RemoveGame(Guid gameId);

    /// <summary>
    /// Returns the IDs of all games currently held in memory.
    /// </summary>
    IReadOnlyCollection<Guid> GetActiveGameIds();

    /// <summary>
    /// Number of games currently held in memory.
    /// </summary>
    int Count { get; }
}
