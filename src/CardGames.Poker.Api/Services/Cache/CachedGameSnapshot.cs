using CardGames.Contracts.SignalR;

namespace CardGames.Poker.Api.Services.Cache;

/// <summary>
/// Holds a cached snapshot of game state that was last broadcast via SignalR.
/// This avoids re-querying the database for state that was just built,
/// especially during broadcast cycles and client reconnections.
/// </summary>
public sealed class CachedGameSnapshot
{
	/// <summary>
	/// The public table state visible to all players.
	/// </summary>
	public required TableStatePublicDto PublicState { get; init; }

	/// <summary>
	/// Per-player private state keyed by user ID (typically email).
	/// </summary>
	public required Dictionary<string, PrivateStateDto> PrivateStates { get; init; }

	/// <summary>
	/// Ordered list of player user IDs in the game.
	/// </summary>
	public required IReadOnlyList<string> PlayerUserIds { get; init; }

	/// <summary>
	/// When this snapshot was created.
	/// </summary>
	public required DateTimeOffset CapturedAt { get; init; }

	/// <summary>
	/// The hand number when this snapshot was captured.
	/// Used to detect stale snapshots across hand boundaries.
	/// </summary>
	public required int HandNumber { get; init; }
}
