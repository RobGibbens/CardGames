using CardGames.Contracts.SignalR;

namespace CardGames.Poker.Api.Services.Cache;

/// <summary>
/// In-memory cache for active-game broadcast snapshots.
/// Stores the last-broadcast public + private state per game so that
/// reconnecting clients and repeated broadcasts avoid database round-trips.
/// </summary>
public interface IActiveGameCache
{
    /// <summary>
    /// Attempts to retrieve the cached snapshot for a game.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="snapshot">The cached snapshot if found.</param>
    /// <returns><c>true</c> if a snapshot exists in the cache; otherwise <c>false</c>.</returns>
    bool TryGet(Guid gameId, out CachedGameSnapshot snapshot);

    /// <summary>
    /// Stores a snapshot in the cache. If the cache already contains a snapshot
    /// for this game with a higher or equal <see cref="CachedGameSnapshot.VersionNumber"/>,
    /// the write is silently rejected (stale-write protection).
    /// </summary>
    /// <param name="snapshot">The snapshot to store.</param>
    void Set(CachedGameSnapshot snapshot);

    /// <summary>
    /// Updates or inserts a single player's private state within an existing snapshot.
    /// Used when a reconnecting client's private state was missing from the cached snapshot.
    /// The write is rejected if <paramref name="versionNumber"/> is older than the stored snapshot.
    /// </summary>
    void UpsertPrivateState(Guid gameId, string userId, PrivateStateDto privateState, ulong versionNumber);

    /// <summary>
    /// Removes the cached snapshot for a game.
    /// </summary>
    /// <returns><c>true</c> if a snapshot was removed; otherwise <c>false</c>.</returns>
    bool Evict(Guid gameId);

    /// <summary>
    /// Removes all snapshots older than the specified cutoff.
    /// </summary>
    /// <returns>The number of entries evicted.</returns>
    int Compact(DateTimeOffset olderThanUtc);

    /// <summary>
    /// Returns the set of game IDs currently held in the cache.
    /// </summary>
    IReadOnlyCollection<Guid> GetActiveGameIds();

    /// <summary>
    /// The number of snapshots currently in the cache.
    /// </summary>
    int Count { get; }
}
