using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using CardGames.Contracts.SignalR;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Services.Cache;

/// <summary>
/// Singleton in-memory cache backed by <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Stores the last-broadcast game state so reconnecting clients and repeated
/// broadcasts avoid database round-trips.
/// </summary>
public sealed class ActiveGameCache : IActiveGameCache
{
    private readonly ConcurrentDictionary<Guid, CachedGameSnapshot> _snapshots = new();
    private readonly IOptions<ActiveGameCacheOptions> _options;
    private readonly ILogger<ActiveGameCache> _logger;

    // Metrics
    private readonly Counter<long> _hitCounter;
    private readonly Counter<long> _missCounter;
    private readonly Counter<long> _evictedCounter;
    private readonly Counter<long> _rejectedStaleWriteCounter;
    private readonly Counter<long> _partialPrivateFallbackCounter;

    public ActiveGameCache(
        IOptions<ActiveGameCacheOptions> options,
        ILogger<ActiveGameCache> logger,
        IMeterFactory meterFactory)
    {
        _options = options;
        _logger = logger;

        var meter = meterFactory.Create("CardGames.Poker.Api.ActiveGameCache");
        _hitCounter = meter.CreateCounter<long>("active_game_cache_hit");
        _missCounter = meter.CreateCounter<long>("active_game_cache_miss");
        _evictedCounter = meter.CreateCounter<long>("active_game_cache_evicted");
        _rejectedStaleWriteCounter = meter.CreateCounter<long>("active_game_cache_rejected_stale_write");
        _partialPrivateFallbackCounter = meter.CreateCounter<long>("active_game_cache_partial_private_fallback");
    }

    /// <inheritdoc />
    public bool TryGet(Guid gameId, out CachedGameSnapshot snapshot)
    {
        if (!_options.Value.Enabled)
        {
            snapshot = default!;
            return false;
        }

        if (_snapshots.TryGetValue(gameId, out snapshot!))
        {
            _hitCounter.Add(1);
            return true;
        }

        _missCounter.Add(1);
        snapshot = default!;
        return false;
    }

    /// <inheritdoc />
    public void Set(CachedGameSnapshot snapshot)
    {
        if (!_options.Value.Enabled)
            return;

        _snapshots.AddOrUpdate(
            snapshot.GameId,
            addValueFactory: _ => snapshot,
            updateValueFactory: (_, existing) =>
            {
                if (snapshot.VersionNumber <= existing.VersionNumber)
                {
                    _rejectedStaleWriteCounter.Add(1);
                    _logger.LogDebug(
                        "Rejected stale snapshot write for game {GameId}: incoming version {Incoming} <= stored version {Stored}",
                        snapshot.GameId, snapshot.VersionNumber, existing.VersionNumber);
                    return existing;
                }

                return snapshot;
            });
    }

    /// <inheritdoc />
    public void UpsertPrivateState(Guid gameId, string userId, PrivateStateDto privateState, ulong versionNumber)
    {
        if (!_options.Value.Enabled)
            return;

        _partialPrivateFallbackCounter.Add(1);

        _snapshots.AddOrUpdate(
            gameId,
            addValueFactory: _ =>
            {
                // No existing snapshot — nothing to upsert into. This is a no-op;
                // the next full broadcast will populate the cache.
                _logger.LogDebug(
                    "UpsertPrivateState called for game {GameId} user {UserId} but no snapshot exists; skipping",
                    gameId, userId);
                return null!;
            },
            updateValueFactory: (_, existing) =>
            {
                if (existing is null)
                    return null!;

                if (versionNumber < existing.VersionNumber)
                {
                    _rejectedStaleWriteCounter.Add(1);
                    return existing;
                }

                // Build a new snapshot with the updated private state
                var updatedPrivateStates = existing.PrivateStatesByUserId.SetItem(userId, privateState);
                return new CachedGameSnapshot
                {
                    GameId = existing.GameId,
                    VersionNumber = existing.VersionNumber,
                    PublicState = existing.PublicState,
                    PrivateStatesByUserId = updatedPrivateStates,
                    PlayerUserIds = existing.PlayerUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase)
                        ? existing.PlayerUserIds
                        : existing.PlayerUserIds.Add(userId),
                    HandNumber = existing.HandNumber,
                    Phase = existing.Phase,
                    BuiltAtUtc = existing.BuiltAtUtc
                };
            });

        // Remove the null entry created by addValueFactory when no snapshot existed
        _snapshots.TryRemove(new KeyValuePair<Guid, CachedGameSnapshot>(gameId, null!));
    }

    /// <inheritdoc />
    public bool Evict(Guid gameId)
    {
        if (_snapshots.TryRemove(gameId, out _))
        {
            _evictedCounter.Add(1);
            _logger.LogDebug("Evicted cached snapshot for game {GameId}", gameId);
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public int Compact(DateTimeOffset olderThanUtc)
    {
        var evicted = 0;
        foreach (var kvp in _snapshots)
        {
            if (kvp.Value.BuiltAtUtc < olderThanUtc)
            {
                if (_snapshots.TryRemove(kvp))
                {
                    evicted++;
                    _evictedCounter.Add(1);
                }
            }
        }

        if (evicted > 0)
        {
            _logger.LogInformation("Compacted {Count} stale snapshot(s) from active game cache", evicted);
        }

        return evicted;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> GetActiveGameIds() => _snapshots.Keys.ToArray();

    /// <inheritdoc />
    public int Count => _snapshots.Count;
}
