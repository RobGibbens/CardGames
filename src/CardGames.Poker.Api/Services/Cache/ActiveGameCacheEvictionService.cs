using CardGames.Poker.Api.Services.InMemoryEngine;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Services.Cache;

/// <summary>
/// Background service that periodically removes stale snapshots from the
/// <see cref="IActiveGameCache"/> to prevent unbounded memory growth from
/// abandoned or completed games whose eviction was missed.
/// Also evicts idle game states from <see cref="IGameStateManager"/> when
/// the in-memory engine is active.
/// </summary>
public sealed class ActiveGameCacheEvictionService : BackgroundService
{
    private readonly IActiveGameCache _cache;
    private readonly IGameStateManager _gameStateManager;
    private readonly IOptions<ActiveGameCacheOptions> _options;
    private readonly IOptions<InMemoryEngineOptions> _engineOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ActiveGameCacheEvictionService> _logger;

    public ActiveGameCacheEvictionService(
        IActiveGameCache cache,
        IGameStateManager gameStateManager,
        IOptions<ActiveGameCacheOptions> options,
        IOptions<InMemoryEngineOptions> engineOptions,
        TimeProvider timeProvider,
        ILogger<ActiveGameCacheEvictionService> logger)
    {
        _cache = cache;
        _gameStateManager = gameStateManager;
        _options = options;
        _engineOptions = engineOptions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ActiveGameCacheEvictionService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.Value;
            try
            {
                await Task.Delay(opts.ScavengeInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (!opts.Enabled)
                continue;

            try
            {
                var cutoff = _timeProvider.GetUtcNow() - opts.MaxSnapshotAge;
                var evicted = _cache.Compact(cutoff);
                _logger.LogDebug(
                    "Cache scavenge complete: evicted {Count} snapshots, {Remaining} remaining",
                    evicted, _cache.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache scavenge");
            }

            // Also evict idle game states from the in-memory engine
            EvictIdleGameStates();
        }

        _logger.LogInformation("ActiveGameCacheEvictionService stopped");
    }

    /// <summary>
    /// Removes game states from <see cref="IGameStateManager"/> that have been idle
    /// longer than <see cref="InMemoryEngineOptions.IdleEvictionAfter"/>.
    /// Games still in the broadcast cache are considered active and are not evicted.
    /// </summary>
    private void EvictIdleGameStates()
    {
        var engineOpts = _engineOptions.Value;
        if (!engineOpts.Enabled)
            return;

        var now = _timeProvider.GetUtcNow();
        var evicted = 0;

        foreach (var gameId in _gameStateManager.GetActiveGameIds())
        {
            // Don't evict games that are still in the broadcast cache (actively being played)
            if (_cache.TryGet(gameId, out _))
                continue;

            if (!_gameStateManager.TryGetGame(gameId, out var state))
                continue;

            var idle = now - state.UpdatedAt;
            if (idle >= engineOpts.IdleEvictionAfter)
            {
                _gameStateManager.RemoveGame(gameId);
                evicted++;
            }
        }

        if (evicted > 0)
        {
            _logger.LogInformation(
                "Evicted {Count} idle game(s) from in-memory engine, {Remaining} remaining",
                evicted, _gameStateManager.Count);
        }
    }
}
