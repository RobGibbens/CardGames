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
}
