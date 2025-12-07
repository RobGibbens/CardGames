namespace CardGames.Poker.Api.Data.Entities;

/// <summary>
/// Represents a single betting action taken by a player during a betting round.
/// </summary>
/// <remarks>
/// <para>
/// BettingActionRecord provides a complete audit trail of all betting actions, including:
/// </para>
/// <list type="bullet">
///   <item><description>The action type (check, bet, call, raise, fold, all-in)</description></item>
///   <item><description>The amount involved in the action</description></item>
///   <item><description>Timestamp for action ordering and timeout tracking</description></item>
///   <item><description>Pre and post-action chip stacks for verification</description></item>
/// </list>
/// <para>
/// This entity enables:
/// </para>
/// <list type="bullet">
///   <item><description>Hand history replay</description></item>
///   <item><description>Dispute resolution</description></item>
///   <item><description>Player statistics and analysis</description></item>
///   <item><description>Anti-fraud auditing</description></item>
/// </list>
/// </remarks>
public class BettingActionRecord : EntityWithRowVersion
{
	/// <summary>
	/// Unique identifier for this action record.
	/// </summary>
	public Guid Id { get; set; } = Guid.CreateVersion7();

	/// <summary>
	/// Foreign key to the betting round.
	/// </summary>
	public Guid BettingRoundId { get; set; }

	/// <summary>
	/// Navigation property to the betting round.
	/// </summary>
	public BettingRound BettingRound { get; set; } = null!;

	/// <summary>
	/// Foreign key to the player who took this action.
	/// </summary>
	public Guid GamePlayerId { get; set; }

	/// <summary>
	/// Navigation property to the acting player.
	/// </summary>
	public GamePlayer GamePlayer { get; set; } = null!;

	/// <summary>
	/// The order of this action within the betting round (1-based).
	/// </summary>
	public int ActionOrder { get; set; }

	/// <summary>
	/// The type of betting action taken.
	/// </summary>
	public BettingActionType ActionType { get; set; }

	/// <summary>
	/// The amount involved in this action.
	/// </summary>
	/// <remarks>
	/// For Check/Fold: 0
	/// For Call: the call amount
	/// For Bet: the bet amount
	/// For Raise: the total raise-to amount
	/// For AllIn: the all-in amount
	/// For Post: the posted amount (ante/blind)
	/// </remarks>
	public int Amount { get; set; }

	/// <summary>
	/// The amount the player actually put into the pot with this action.
	/// </summary>
	/// <remarks>
	/// This differs from Amount for calls (when Amount is call amount but chips moved = call amount - current bet).
	/// </remarks>
	public int ChipsMoved { get; set; }

	/// <summary>
	/// The player's chip stack before this action.
	/// </summary>
	public int ChipStackBefore { get; set; }

	/// <summary>
	/// The player's chip stack after this action.
	/// </summary>
	public int ChipStackAfter { get; set; }

	/// <summary>
	/// The pot size before this action.
	/// </summary>
	public int PotBefore { get; set; }

	/// <summary>
	/// The pot size after this action.
	/// </summary>
	public int PotAfter { get; set; }

	/// <summary>
	/// Time in seconds the player took to make this decision.
	/// </summary>
	public double? DecisionTimeSeconds { get; set; }

	/// <summary>
	/// Indicates whether this action was forced (ante, blind, bring-in).
	/// </summary>
	public bool IsForced { get; set; }

	/// <summary>
	/// Indicates whether this action timed out (auto-folded or auto-checked).
	/// </summary>
	public bool IsTimeout { get; set; }

	/// <summary>
	/// Optional note or metadata about this action.
	/// </summary>
	public string? Note { get; set; }

	/// <summary>
	/// The date and time when this action was recorded.
	/// </summary>
	public DateTimeOffset ActionAt { get; set; }
}

/// <summary>
/// The type of betting action taken by a player.
/// </summary>
/// <remarks>
/// Values match the CardGames.Poker.Betting.BettingActionType enum for compatibility.
/// </remarks>
public enum BettingActionType
{
	/// <summary>Pass the action without betting when no bet has been made.</summary>
	Check = 0,
	
	/// <summary>Make an initial bet in the current betting round.</summary>
	Bet = 1,
	
	/// <summary>Match the current highest bet.</summary>
	Call = 2,
	
	/// <summary>Increase the current bet amount.</summary>
	Raise = 3,
	
	/// <summary>Give up the hand and forfeit any chips already in the pot.</summary>
	Fold = 4,
	
	/// <summary>Bet all remaining chips.</summary>
	AllIn = 5,
	
	/// <summary>Post ante or blind (forced bet).</summary>
	Post = 6
}
