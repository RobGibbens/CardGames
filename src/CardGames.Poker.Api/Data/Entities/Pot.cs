namespace CardGames.Poker.Api.Data.Entities;

/// <summary>
/// Represents a pot in a poker game, including the main pot and side pots.
/// </summary>
/// <remarks>
/// <para>
/// The Pot entity tracks the pot structure in a hand, supporting:
/// </para>
/// <list type="bullet">
///   <item><description>Main pot with all active players eligible</description></item>
///   <item><description>Side pots when players go all-in for different amounts</description></item>
///   <item><description>Pot matching mechanics (Kings and Lows variant)</description></item>
///   <item><description>Split pot tracking when multiple players tie</description></item>
/// </list>
/// <para>
/// Each hand can have multiple Pot records. The main pot is created first,
/// and side pots are created as players go all-in for different amounts.
/// </para>
/// </remarks>
public class Pot : EntityWithRowVersion
{
	/// <summary>
	/// Unique identifier for this pot.
	/// </summary>
	public Guid Id { get; set; } = Guid.CreateVersion7();

	/// <summary>
	/// Foreign key to the game.
	/// </summary>
	public Guid GameId { get; set; }

	/// <summary>
	/// Navigation property to the game.
	/// </summary>
	public Game Game { get; set; } = null!;

	/// <summary>
	/// The hand number this pot belongs to.
	/// </summary>
	public int HandNumber { get; set; }

	/// <summary>
	/// The type of pot.
	/// </summary>
	public PotType PotType { get; set; }

	/// <summary>
	/// Order of the pot (0 for main pot, 1+ for side pots in order).
	/// </summary>
	public int PotOrder { get; set; }

	/// <summary>
	/// The total amount in this pot.
	/// </summary>
	public int Amount { get; set; }

	/// <summary>
	/// The maximum contribution per player for this pot (for side pot calculation).
	/// </summary>
	/// <remarks>
	/// For the main pot, this is the smallest all-in amount.
	/// For side pots, this is the additional amount per eligible player.
	/// </remarks>
	public int? MaxContributionPerPlayer { get; set; }

	/// <summary>
	/// Indicates whether this pot has been awarded.
	/// </summary>
	public bool IsAwarded { get; set; }

	/// <summary>
	/// The date and time when this pot was awarded.
	/// </summary>
	public DateTimeOffset? AwardedAt { get; set; }

	/// <summary>
	/// JSON-serialized list of winner player IDs and their payouts.
	/// </summary>
	/// <remarks>
	/// Example: [{"playerId": "guid1", "amount": 150}, {"playerId": "guid2", "amount": 150}]
	/// Used to track pot splits and individual payouts.
	/// </remarks>
	public string? WinnerPayouts { get; set; }

	/// <summary>
	/// Reason for winning (for display purposes).
	/// </summary>
	/// <remarks>
	/// Examples: "Full House, Kings over Sevens", "All others folded", "Tie - Two Pair, Aces and Kings"
	/// </remarks>
	public string? WinReason { get; set; }

	/// <summary>
	/// The date and time when this pot was created.
	/// </summary>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// Navigation property for contributions to this pot.
	/// </summary>
	public ICollection<PotContribution> Contributions { get; set; } = [];
}

/// <summary>
/// The type of pot in a poker game.
/// </summary>
public enum PotType
{
	/// <summary>
	/// The main pot that all active players compete for.
	/// </summary>
	Main = 0,

	/// <summary>
	/// A side pot created when a player goes all-in for less than the current bet.
	/// </summary>
	Side = 1,

	/// <summary>
	/// A pot that carries over from a previous hand (Kings and Lows dead hand).
	/// </summary>
	Carryover = 2
}
