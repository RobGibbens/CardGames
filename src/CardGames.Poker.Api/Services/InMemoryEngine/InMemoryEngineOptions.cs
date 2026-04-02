namespace CardGames.Poker.Api.Services.InMemoryEngine;

/// <summary>
/// Configuration options for the in-memory game engine (Phase 4).
/// </summary>
public sealed class InMemoryEngineOptions
{
    public const string SectionName = "InMemoryEngine";

    /// <summary>
    /// Master kill switch. When <c>false</c>, all command handlers fall back
    /// to the existing <c>CardsDbContext</c> code path.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Interval for the background periodic checkpoint service to flush dirty game
    /// states to the database.
    /// </summary>
    public TimeSpan CheckpointInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Idle game states older than this are evicted from the in-memory engine.
    /// </summary>
    public TimeSpan IdleEvictionAfter { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// How often the scavenger runs to evict idle games.
    /// </summary>
    public TimeSpan ScavengeInterval { get; set; } = TimeSpan.FromMinutes(5);
}
