namespace CardGames.Poker.Api.Data.Entities;

/// <summary>
/// Represents a player's contribution to a specific pot.
/// </summary>
/// <remarks>
/// <para>
/// PotContribution tracks how much each player has contributed to each pot.
/// This is essential for:
/// </para>
/// <list type="bullet">
///   <item><description>Calculating side pots when players go all-in</description></item>
///   <item><description>Determining pot eligibility at showdown</description></item>
///   <item><description>Auditing and replaying hand history</description></item>
///   <item><description>Tracking pot matching in Kings and Lows variant</description></item>
/// </list>
/// <para>
/// Each player may have contributions to multiple pots in a single hand
/// (main pot plus any side pots they're eligible for).
/// </para>
/// </remarks>
public class PotContribution : EntityWithRowVersion
{
	/// <summary>
	/// Unique identifier for this contribution record.
	/// </summary>
	public Guid Id { get; set; } = Guid.CreateVersion7();

	/// <summary>
	/// Foreign key to the pot.
	/// </summary>
	public Guid PotId { get; set; }

	/// <summary>
	/// Navigation property to the pot.
	/// </summary>
	public Pot Pot { get; set; } = null!;

	/// <summary>
	/// Foreign key to the contributing player.
	/// </summary>
	public Guid GamePlayerId { get; set; }

	/// <summary>
	/// Navigation property to the contributing player.
	/// </summary>
	public GamePlayer GamePlayer { get; set; } = null!;

	/// <summary>
	/// The total amount this player has contributed to this pot.
	/// </summary>
	public int Amount { get; set; }

	/// <summary>
	/// Indicates whether this player is eligible to win this pot.
	/// </summary>
	/// <remarks>
	/// A player may contribute to a pot but not be eligible to win it
	/// (e.g., if they folded, or in a side pot they're not part of).
	/// </remarks>
	public bool IsEligibleToWin { get; set; } = true;

	/// <summary>
	/// Indicates whether this contribution was from a pot matching requirement.
	/// </summary>
	/// <remarks>
	/// Used in Kings and Lows variant where losers must match the pot.
	/// </remarks>
	public bool IsPotMatch { get; set; }

	/// <summary>
	/// The date and time when this contribution was recorded.
	/// </summary>
	public DateTimeOffset ContributedAt { get; set; }
}
