using System.Collections.Immutable;
using CardGames.Contracts.SignalR;

namespace CardGames.Poker.Api.Services.Cache;

/// <summary>
/// Immutable snapshot of the last-broadcast game state, stored in the active game cache.
/// Contains both public state (visible to all) and per-player private states.
/// </summary>
public sealed class CachedGameSnapshot
{
    /// <summary>
    /// The game this snapshot belongs to.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// Monotonic version derived from SQL Server rowversion on <c>Game</c>.
    /// Used for compare-and-swap: the cache rejects writes with a version
    /// less than or equal to the currently stored version.
    /// </summary>
    public required ulong VersionNumber { get; init; }

    /// <summary>
    /// The public table state broadcast to all players in the game group.
    /// </summary>
    public required TableStatePublicDto PublicState { get; init; }

    /// <summary>
    /// Per-player private states keyed by the SignalR user identifier (case-insensitive).
    /// </summary>
    public required ImmutableDictionary<string, PrivateStateDto> PrivateStatesByUserId { get; init; }

    /// <summary>
    /// Ordered list of distinct player user IDs present in this game.
    /// </summary>
    public required ImmutableArray<string> PlayerUserIds { get; init; }

    /// <summary>
    /// The hand number at the time this snapshot was built.
    /// </summary>
    public required int HandNumber { get; init; }

    /// <summary>
    /// The game phase at the time this snapshot was built.
    /// </summary>
    public required string Phase { get; init; }

    /// <summary>
    /// UTC timestamp when this snapshot was built.
    /// </summary>
    public required DateTimeOffset BuiltAtUtc { get; init; }
}
