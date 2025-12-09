namespace CardGames.Poker.Api.Data.Entities;

/// <summary>
/// Represents a betting round within a poker hand.
/// </summary>
/// <remarks>
/// <para>
/// BettingRound tracks the state of a single betting round, including:
/// </para>
/// <list type="bullet">
///   <item><description>The street/phase (pre-flop, flop, third street, etc.)</description></item>
///   <item><description>Current bet to match</description></item>
///   <item><description>Number of raises allowed and made</description></item>
///   <item><description>Active player tracking</description></item>
///   <item><description>Round completion status</description></item>
/// </list>
/// <para>
/// Different poker variants have different numbers of betting rounds:
/// </para>
/// <list type="bullet">
///   <item><description>Five Card Draw: 2 rounds (before and after draw)</description></item>
///   <item><description>Hold'em/Omaha: 4 rounds (pre-flop, flop, turn, river)</description></item>
///   <item><description>Stud games: 5 rounds (3rd through 7th street)</description></item>
/// </list>
/// </remarks>
public class BettingRound : EntityWithRowVersion
{
	/// <summary>
	/// Unique identifier for this betting round.
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
	/// The hand number this betting round belongs to.
	/// </summary>
	public int HandNumber { get; set; }

	/// <summary>
	/// The order of this round within the hand (1-based).
	/// </summary>
	public int RoundNumber { get; set; }

	/// <summary>
	/// The street/phase name for this betting round.
	/// </summary>
	/// <remarks>
	/// Examples: "PreFlop", "Flop", "Turn", "River", "ThirdStreet", "FourthStreet",
	/// "FirstBettingRound", "SecondBettingRound"
	/// </remarks>
	public required string Street { get; set; }

	/// <summary>
	/// The current bet that players must match to stay in the hand.
	/// </summary>
	public int CurrentBet { get; set; }

	/// <summary>
	/// The minimum bet or raise amount for this round.
	/// </summary>
	public int MinBet { get; set; }

	/// <summary>
	/// The number of raises made in this round.
	/// </summary>
	public int RaiseCount { get; set; }

	/// <summary>
	/// Maximum number of raises allowed per round (0 = unlimited).
	/// </summary>
	public int MaxRaises { get; set; }

	/// <summary>
	/// The last raise amount (for tracking min-raise in no-limit).
	/// </summary>
	public int LastRaiseAmount { get; set; }

	/// <summary>
	/// Number of players still in the hand at round start.
	/// </summary>
	public int PlayersInHand { get; set; }

	/// <summary>
	/// Number of players who have acted in this round.
	/// </summary>
	public int PlayersActed { get; set; }

	/// <summary>
	/// Index of the player who must act next.
	/// </summary>
	public int CurrentActorIndex { get; set; }

	/// <summary>
	/// Index of the last player who bet or raised.
	/// </summary>
	public int LastAggressorIndex { get; set; } = -1;

	/// <summary>
	/// Indicates whether this betting round is complete.
	/// </summary>
	public bool IsComplete { get; set; }

	/// <summary>
	/// The date and time when this round started.
	/// </summary>
	public DateTimeOffset StartedAt { get; set; }

	/// <summary>
	/// The date and time when this round completed.
	/// </summary>
	public DateTimeOffset? CompletedAt { get; set; }

	/// <summary>
	/// Navigation property for actions in this round.
	/// </summary>
	public ICollection<BettingActionRecord> Actions { get; set; } = [];
}
