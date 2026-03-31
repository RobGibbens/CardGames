namespace CardGames.Poker.Api.Services.Cache;

/// <summary>
/// In-memory cache for active game state, eliminating redundant database queries
/// during gameplay. Holds the last-broadcast state per game so that broadcast cycles
/// and client reconnections can be served from memory instead of SQL Server.
/// </summary>
/// <remarks>
/// <para>
/// This cache is designed to solve the core scalability issue identified in
/// docs/DatabaseCalls.md: SQL Server functioning as a real-time state engine
/// during active gameplay. By caching the last-broadcast state per game, we
/// eliminate the N+1 query pattern where each broadcast rebuilds full public
/// state + per-player private state from the database.
/// </para>
/// <para>
/// Thread safety: All operations are thread-safe for concurrent access from
/// multiple SignalR connections, background services, and MediatR handlers.
/// </para>
/// </remarks>
public interface IActiveGameCache
{
	/// <summary>
	/// Stores a game state snapshot in the cache.
	/// Called after building state from the database during a broadcast cycle.
	/// </summary>
	/// <param name="gameId">The unique identifier of the game.</param>
	/// <param name="snapshot">The snapshot to cache.</param>
	void Set(Guid gameId, CachedGameSnapshot snapshot);

	/// <summary>
	/// Attempts to retrieve a cached game state snapshot.
	/// </summary>
	/// <param name="gameId">The unique identifier of the game.</param>
	/// <param name="snapshot">The cached snapshot if found.</param>
	/// <returns>True if a cached snapshot was found; false otherwise.</returns>
	bool TryGet(Guid gameId, out CachedGameSnapshot? snapshot);

	/// <summary>
	/// Removes the cached state for a game.
	/// Called on hand completion, game end, or game deletion.
	/// </summary>
	/// <param name="gameId">The unique identifier of the game.</param>
	void Evict(Guid gameId);

	/// <summary>
	/// Gets all game IDs currently in the cache.
	/// Useful for monitoring and diagnostics.
	/// </summary>
	IReadOnlyCollection<Guid> GetActiveGameIds();

	/// <summary>
	/// Gets the current number of cached games.
	/// </summary>
	int Count { get; }
}
