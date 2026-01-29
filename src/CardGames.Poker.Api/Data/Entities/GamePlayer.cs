namespace CardGames.Poker.Api.Data.Entities;

/// <summary>
/// Represents a player's participation and state within a specific game session.
/// </summary>
/// <remarks>
/// <para>
/// GamePlayer is the junction entity between <see cref="Game"/> and <see cref="Player"/>,
/// storing per-game state such as:
/// </para>
/// <list type="bullet">
///   <item><description>Seat position at the table</description></item>
///   <item><description>Current chip stack within this game</description></item>
///   <item><description>Current hand status (folded, all-in, active)</description></item>
///   <item><description>Current bet amount in the active betting round</description></item>
///   <item><description>Game-variant-specific state (e.g., drop/stay decision)</description></item>
/// </list>
/// <para>
/// Each player has one GamePlayer record per game they participate in.
/// This design separates persistent player identity from per-session game state.
/// </para>
/// </remarks>
public class GamePlayer : EntityWithRowVersion
{
	/// <summary>
	/// Unique identifier for this game participation record.
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
	/// Foreign key to the player.
	/// </summary>
	public Guid PlayerId { get; set; }

	/// <summary>
	/// Navigation property to the player.
	/// </summary>
	public Player Player { get; set; } = null!;

	/// <summary>
	/// Zero-based seat position at the table.
	/// </summary>
	public int SeatPosition { get; set; }

	/// <summary>
	/// Current chip stack within this game.
	/// </summary>
	public int ChipStack { get; set; }

	/// <summary>
	/// Initial chip stack when joining the game.
	/// </summary>
	public int StartingChips { get; set; }

	/// <summary>
	/// Current bet amount in the active betting round.
	/// </summary>
	public int CurrentBet { get; set; }

	/// <summary>
	/// Total amount contributed to pots this hand.
	/// </summary>
	public int TotalContributedThisHand { get; set; }

	/// <summary>
	/// Indicates whether the player has folded in the current hand.
	/// </summary>
	public bool HasFolded { get; set; }

	/// <summary>
	/// Indicates whether the player is all-in (zero chips but not folded).
	/// </summary>
	public bool IsAllIn { get; set; }

	/// <summary>
	/// Indicates whether the player is actively connected to the game.
	/// </summary>
	public bool IsConnected { get; set; } = true;

	/// <summary>
	/// Indicates whether this player is marked as sitting out.
	/// </summary>
	public bool IsSittingOut { get; set; }

	/// <summary>
	/// The player's decision for drop-or-stay mechanics (Kings and Lows variant).
	/// </summary>
	public DropOrStayDecision? DropOrStayDecision { get; set; }

	/// <summary>
	/// Indicates whether this player should automatically drop during the DropOrStay phase.
	/// Set to true when a player fails to add sufficient chips during the chip check pause.
	/// </summary>
	public bool AutoDropOnDropOrStay { get; set; }

	/// <summary>
	/// Indicates whether the player has completed their draw in a draw phase.
	/// </summary>
	public bool HasDrawnThisRound { get; set; }

	/// <summary>
	/// The hand number when the player joined (for late entry tracking).
	/// </summary>
	public int JoinedAtHandNumber { get; set; } = 1;

	/// <summary>
	/// The hand number when the player left (-1 if still in game).
	/// </summary>
	public int LeftAtHandNumber { get; set; } = -1;

	/// <summary>
	/// Final chip count when player left the game.
	/// </summary>
	public int? FinalChipCount { get; set; }

	/// <summary>
	/// Chips pending to be added to the player's stack.
	/// Applied automatically when the game status reaches BetweenHands for certain game types.
	/// </summary>
	public int PendingChipsToAdd { get; set; }

	/// <summary>
	/// The player's current status in the game.
	/// </summary>
	public GamePlayerStatus Status { get; set; } = GamePlayerStatus.Active;

	/// <summary>
	/// JSON-serialized variant-specific player state.
	/// </summary>
	/// <remarks>
	/// Examples:
	/// - Baseball: {"pendingBuyCardOffers": [1, 3]}
	/// - KingsAndLows: {"lossesToMatch": 150}
	/// </remarks>
	public string? VariantState { get; set; }

	/// <summary>
	/// The date and time when the player joined this game.
	/// </summary>
	public DateTimeOffset JoinedAt { get; set; }

	/// <summary>
	/// The date and time when the player left this game (null if still active).
	/// </summary>
	public DateTimeOffset? LeftAt { get; set; }

	/// <summary>
	/// Navigation property for cards dealt to this player.
	/// </summary>
	public ICollection<GameCard> Cards { get; set; } = [];

	/// <summary>
	/// Navigation property for pot contributions.
	/// </summary>
	public ICollection<PotContribution> PotContributions { get; set; } = [];

	/// <summary>
	/// Navigation property for betting actions.
	/// </summary>
	public ICollection<BettingActionRecord> BettingActions { get; set; } = [];
}

/// <summary>
/// The status of a player within a game session.
/// </summary>
public enum GamePlayerStatus
{
	/// <summary>
	/// Player is actively participating in the game.
	/// </summary>
	Active = 0,

	/// <summary>
	/// Player has been eliminated (no chips remaining).
	/// </summary>
	Eliminated = 1,

	/// <summary>
	/// Player left the game voluntarily.
	/// </summary>
	Left = 2,

	/// <summary>
	/// Player disconnected and timed out.
	/// </summary>
	Disconnected = 3,

	/// <summary>
	/// Player is sitting out but still in the game.
	/// </summary>
	SittingOut = 4
}

/// <summary>
/// Decision for drop-or-stay mechanics (used in Kings and Lows variant).
/// </summary>
public enum DropOrStayDecision
{
	/// <summary>
	/// Player hasn't made a decision yet.
	/// </summary>
	Undecided = 0,

	/// <summary>
	/// Player chose to stay in the hand.
	/// </summary>
	Stay = 1,

	/// <summary>
	/// Player chose to drop from the hand.
	/// </summary>
	Drop = 2
}
