namespace CardGames.Poker.Api.Services.Cache;

/// <summary>
/// Configuration options for the active game snapshot cache.
/// Provides kill-switch flags so the cache can be disabled without reverting code.
/// </summary>
public sealed class ActiveGameCacheOptions
{
    public const string SectionName = "ActiveGameCache";

    /// <summary>
    /// Master switch: when false, the cache is never written to or read from.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// When true, GameHub reconnect and BroadcastGameStateToUserAsync serve
    /// cached snapshots instead of rebuilding from the database.
    /// </summary>
    public bool ServeReconnectsFromCache { get; init; } = true;

    /// <summary>
    /// Snapshots older than this are eligible for background eviction.
    /// </summary>
    public TimeSpan IdleEvictionAfter { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Hard age limit: snapshots exceeding this age are evicted regardless of activity.
    /// </summary>
    public TimeSpan MaxSnapshotAge { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How often the background eviction service runs.
    /// </summary>
    public TimeSpan ScavengeInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// When true, ContinuousPlayBackgroundService uses adaptive polling intervals:
    /// fast (1 s) when cached active games exist, slow (discovery) when the cache is empty.
    /// </summary>
    public bool AdaptivePollingEnabled { get; init; } = true;

    /// <summary>
    /// Polling interval when no active games are cached.
    /// Must be long enough to avoid unnecessary DB load but short enough
    /// to discover games after a cold start or deployment.
    /// </summary>
    public TimeSpan SlowPollInterval { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// When true, ActionTimerService reduces per-second broadcasts to
    /// start/stop events plus periodic sync heartbeats.
    /// Requires the web client to run a local countdown from StartedAtUtc + DurationSeconds.
    /// </summary>
    public bool ReduceTimerBroadcasts { get; init; } = true;

    /// <summary>
    /// Interval between sync heartbeat broadcasts when <see cref="ReduceTimerBroadcasts"/> is true.
    /// Provides drift protection for clients with inaccurate clocks.
    /// </summary>
    public TimeSpan TimerSyncHeartbeatInterval { get; init; } = TimeSpan.FromSeconds(5);
}
