namespace CardGames.Poker.Api.Data.Entities;

/// <summary>
/// Represents a persistent player identity across multiple game sessions.
/// </summary>
/// <remarks>
/// <para>
/// The Player entity stores the identity of a player separate from their per-game state.
/// This enables:
/// </para>
/// <list type="bullet">
///   <item><description>Tracking player history across multiple games</description></item>
///   <item><description>Storing player preferences and settings</description></item>
///   <item><description>Aggregating statistics and achievements</description></item>
///   <item><description>Supporting user authentication and profiles</description></item>
/// </list>
/// <para>
/// Per-game state (chip stack, position, fold status) is stored in <see cref="GamePlayer"/>.
/// </para>
/// </remarks>
public class Player : EntityWithRowVersion
{
	/// <summary>
	/// Unique identifier for the player.
	/// </summary>
	public Guid Id { get; set; } = Guid.CreateVersion7();

	/// <summary>
	/// Display name of the player.
	/// </summary>
	public required string Name { get; set; }

	/// <summary>
	/// Optional email address for the player (for authentication/notifications).
	/// </summary>
	public string? Email { get; set; }

	/// <summary>
	/// Optional external identity provider ID (for OAuth/SSO integration).
	/// </summary>
	public string? ExternalId { get; set; }

	/// <summary>
	/// Optional avatar URL for the player.
	/// </summary>
	public string? AvatarUrl { get; set; }

	/// <summary>
	/// Indicates whether the player account is active.
	/// </summary>
	public bool IsActive { get; set; } = true;

	/// <summary>
	/// JSON-serialized player preferences and settings.
	/// </summary>
	/// <remarks>
	/// Examples: {"autoMuck": true, "defaultBuyIn": 1000, "preferredVariants": ["HOLDEM", "OMAHA"]}
	/// </remarks>
	public string? Preferences { get; set; }

	/// <summary>
	/// Total number of games the player has participated in.
	/// </summary>
	public int TotalGamesPlayed { get; set; }

	/// <summary>
	/// Total number of hands the player has played.
	/// </summary>
	public int TotalHandsPlayed { get; set; }

	/// <summary>
	/// Total number of hands the player has won.
	/// </summary>
	public int TotalHandsWon { get; set; }

	/// <summary>
	/// Total chips won across all games.
	/// </summary>
	public long TotalChipsWon { get; set; }

	/// <summary>
	/// Total chips lost across all games.
	/// </summary>
	public long TotalChipsLost { get; set; }

	/// <summary>
	/// The date and time when the player was created.
	/// </summary>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// The date and time when the player was last updated.
	/// </summary>
	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	/// The date and time when the player was last active.
	/// </summary>
	public DateTimeOffset? LastActiveAt { get; set; }

	/// <summary>
	/// Navigation property for game participations.
	/// </summary>
	public ICollection<GamePlayer> GameParticipations { get; set; } = [];
}
