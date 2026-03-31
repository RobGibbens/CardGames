using System.Collections.Concurrent;

namespace CardGames.Poker.Api.Services.Cache;

/// <summary>
/// Thread-safe in-memory cache for active game state using <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// Stores the last-broadcast game state snapshot per game to avoid redundant database queries
/// during broadcast cycles and client reconnections.
/// </summary>
/// <remarks>
/// <para>
/// This is a singleton service. All game state snapshots are held in memory for the
/// lifetime of the server process. Snapshots are evicted on hand completion, game end,
/// or game deletion, so memory usage is bounded by the number of active games.
/// </para>
/// <para>
/// For a single-instance deployment, this is sufficient. For multi-instance (horizontal
/// scale-out), this cache should be backed by Redis using the existing distributed cache
/// infrastructure. The interface allows swapping implementations without changing consumers.
/// </para>
/// </remarks>
public sealed class ActiveGameCache : IActiveGameCache
{
	private readonly ConcurrentDictionary<Guid, CachedGameSnapshot> _cache = new();
	private readonly ILogger<ActiveGameCache> _logger;

	public ActiveGameCache(ILogger<ActiveGameCache> logger)
	{
		_logger = logger;
	}

	/// <inheritdoc />
	public void Set(Guid gameId, CachedGameSnapshot snapshot)
	{
		_cache[gameId] = snapshot;
		_logger.LogDebug(
			"Cached game state for game {GameId}, hand #{HandNumber}, {PlayerCount} players",
			gameId, snapshot.HandNumber, snapshot.PlayerUserIds.Count);
	}

	/// <inheritdoc />
	public bool TryGet(Guid gameId, out CachedGameSnapshot? snapshot)
	{
		var found = _cache.TryGetValue(gameId, out snapshot);
		_logger.LogDebug(
			"Cache {Result} for game {GameId}",
			found ? "hit" : "miss", gameId);
		return found;
	}

	/// <inheritdoc />
	public void Evict(Guid gameId)
	{
		if (_cache.TryRemove(gameId, out _))
		{
			_logger.LogInformation("Evicted cached game state for game {GameId}", gameId);
		}
	}

	/// <inheritdoc />
	public IReadOnlyCollection<Guid> GetActiveGameIds()
	{
		return _cache.Keys.ToArray();
	}

	/// <inheritdoc />
	public int Count => _cache.Count;
}
