namespace CardGames.Poker.Api.Data.Entities;

/// <summary>
/// Represents a poker game session with its current state and configuration.
/// </summary>
/// <remarks>
/// <para>
/// The Game entity is the central record for a poker game session. It tracks:
/// </para>
/// <list type="bullet">
///   <item><description>The game type and variant-specific configuration</description></item>
///   <item><description>Current game phase and hand number</description></item>
///   <item><description>Betting parameters (ante, blinds, bet sizes)</description></item>
///   <item><description>Dealer position and button tracking</description></item>
///   <item><description>Total pot and game state</description></item>
/// </list>
/// <para>
/// A Game can have multiple hands played within it. Each hand involves dealing cards,
/// betting rounds, and potentially a showdown. The <see cref="CurrentHandNumber"/> tracks
/// how many hands have been played in this session.
/// </para>
/// </remarks>
public class Game : EntityWithRowVersion
{
	/// <summary>
	/// Unique identifier for the game session.
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Foreign key to the game type definition.
	/// </summary>
	public Guid GameTypeId { get; set; }

	/// <summary>
	/// Navigation property to the game type definition.
	/// </summary>
	public GameType GameType { get; set; } = null!;

	/// <summary>
	/// Optional friendly name for the game session.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Current phase of the game (variant-specific).
	/// Stored as string for flexibility across different game variants.
	/// </summary>
	/// <remarks>
	/// Examples: "WaitingToStart", "CollectingAntes", "Dealing", "PreFlop", "Flop", 
	/// "ThirdStreet", "DrawPhase", "Showdown", "Complete"
	/// </remarks>
	public required string CurrentPhase { get; set; }

	/// <summary>
	/// Current hand number being played (1-based).
	/// </summary>
	public int CurrentHandNumber { get; set; } = 1;

	/// <summary>
	/// Zero-based index of the current dealer position.
	/// </summary>
	public int DealerPosition { get; set; }

	/// <summary>
	/// Ante amount required from each player (if applicable).
	/// </summary>
	public int? Ante { get; set; }

	/// <summary>
	/// Small blind amount (for blind-based games).
	/// </summary>
	public int? SmallBlind { get; set; }

	/// <summary>
	/// Big blind amount (for blind-based games).
	/// </summary>
	public int? BigBlind { get; set; }

	/// <summary>
	/// Bring-in amount (for stud games with bring-in).
	/// </summary>
	public int? BringIn { get; set; }

	/// <summary>
	/// Small bet size (for structured limit games - typically 3rd/4th street).
	/// </summary>
	public int? SmallBet { get; set; }

	/// <summary>
	/// Big bet size (for structured limit games - typically 5th-7th street).
	/// </summary>
	public int? BigBet { get; set; }

	/// <summary>
	/// Minimum bet for no-limit/pot-limit games.
	/// </summary>
	public int? MinBet { get; set; }

	/// <summary>
	/// JSON-serialized game-specific settings (e.g., buy-card price, king-required rule).
	/// </summary>
	/// <remarks>
	/// This property stores variant-specific configuration that doesn't fit standard columns.
	/// Examples:
	/// - Baseball: {"buyCardPrice": 20}
	/// - KingsAndLows: {"kingRequired": true, "anteEveryHand": false}
	/// </remarks>
	public string? GameSettings { get; set; }

	/// <summary>
	/// The current status of the game session.
	/// </summary>
	public GameStatus Status { get; set; } = GameStatus.WaitingForPlayers;

	/// <summary>
	/// Index of the current player who needs to act (-1 if no action pending).
	/// </summary>
	public int CurrentPlayerIndex { get; set; } = -1;

	/// <summary>
	/// Index of the bring-in player for stud games (-1 if not applicable).
	/// </summary>
	public int BringInPlayerIndex { get; set; } = -1;

	/// <summary>
	/// Index of the next player to draw in draw phase (-1 if not in draw phase).
	/// </summary>
	public int CurrentDrawPlayerIndex { get; set; } = -1;

	/// <summary>
	/// Seed used for deck shuffling (for reproducibility/auditing).
	/// </summary>
	public int? RandomSeed { get; set; }

	/// <summary>
	/// The date and time when the game was created.
	/// </summary>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// The date and time when the game was last updated.
	/// </summary>
	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	/// The date and time when the game started (first hand dealt).
	/// </summary>
	public DateTimeOffset? StartedAt { get; set; }

	/// <summary>
	/// The date and time when the game ended.
	/// </summary>
	public DateTimeOffset? EndedAt { get; set; }

	/// <summary>
	/// The date and time when the current hand was completed (showdown finished).
	/// Used to track the start of the results display period.
	/// </summary>
	public DateTimeOffset? HandCompletedAt { get; set; }

	/// <summary>
	/// The date and time when the next hand is scheduled to start.
	/// Calculated as HandCompletedAt + results display duration (typically 7 seconds).
	/// </summary>
	public DateTimeOffset? NextHandStartsAt { get; set; }

	/// <summary>
	/// The unique identifier of the user who created this game (host).
	/// </summary>
	public string? CreatedById { get; set; }

	/// <summary>
	/// The name or email of the user who created this game (host).
	/// </summary>
	public string? CreatedByName { get; set; }

	/// <summary>
	/// The unique identifier of the user who last updated this game.
	/// </summary>
	public string? UpdatedById { get; set; }

	/// <summary>
	/// The name or email of the user who last updated this game.
	/// </summary>
	public string? UpdatedByName { get; set; }

	/// <summary>
	/// Navigation property for players in this game.
	/// </summary>
	public ICollection<GamePlayer> GamePlayers { get; set; } = [];

	/// <summary>
	/// Navigation property for cards in this game.
	/// </summary>
	public ICollection<GameCard> GameCards { get; set; } = [];

	/// <summary>
	/// Navigation property for pots in this game.
	/// </summary>
	public ICollection<Pot> Pots { get; set; } = [];

	/// <summary>
	/// Navigation property for betting rounds in this game.
	/// </summary>
	public ICollection<BettingRound> BettingRounds { get; set; } = [];
}

/// <summary>
/// The overall status of a game session.
/// </summary>
public enum GameStatus
{
	/// <summary>
	/// Game is created but waiting for enough players to join.
	/// </summary>
	WaitingForPlayers = 0,

	/// <summary>
	/// Game is in progress with an active hand.
	/// </summary>
	InProgress = 1,

	/// <summary>
	/// Game is paused between hands.
	/// </summary>
	BetweenHands = 2,

	/// <summary>
	/// Game has ended normally.
	/// </summary>
	Completed = 3,

	/// <summary>
	/// Game was cancelled or abandoned.
	/// </summary>
	Cancelled = 4
}
