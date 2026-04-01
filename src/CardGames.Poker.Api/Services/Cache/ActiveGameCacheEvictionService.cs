using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Services.Cache;

/// <summary>
/// Background service that periodically removes stale snapshots from the
/// <see cref="IActiveGameCache"/> to prevent unbounded memory growth from
/// abandoned or completed games whose eviction was missed.
/// </summary>
public sealed class ActiveGameCacheEvictionService : BackgroundService
{
    private readonly IActiveGameCache _cache;
    private readonly IOptions<ActiveGameCacheOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ActiveGameCacheEvictionService> _logger;

    public ActiveGameCacheEvictionService(
        IActiveGameCache cache,
        IOptions<ActiveGameCacheOptions> options,
        TimeProvider timeProvider,
        ILogger<ActiveGameCacheEvictionService> logger)
    {
        _cache = cache;
        _options = options;
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
        }

        _logger.LogInformation("ActiveGameCacheEvictionService stopped");
    }
}
