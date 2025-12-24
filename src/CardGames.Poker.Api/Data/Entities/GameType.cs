namespace CardGames.Poker.Api.Data.Entities;

/// <summary>
/// Defines a poker game variant with its rules and constraints.
/// This entity stores the static definition of game types (e.g., Texas Hold'em, Seven Card Stud, Baseball).
/// </summary>
/// <remarks>
/// <para>
/// GameType serves as a template for creating game sessions. It defines:
/// </para>
/// <list type="bullet">
///   <item><description>The betting structure (antes, blinds, bring-ins)</description></item>
///   <item><description>Player limits (min/max players)</description></item>
///   <item><description>Card dealing patterns (hole cards, community cards, board cards)</description></item>
///   <item><description>Wild card rules specific to the variant</description></item>
///   <item><description>Special mechanics (draw phases, buy-card options, etc.)</description></item>
/// </list>
/// <para>
/// This entity is designed to be extensible for future game variants without schema changes.
/// Game-specific settings are stored as JSON in the <see cref="VariantSettings"/> property.
/// </para>
/// </remarks>
public class GameType : EntityWithRowVersion
{
	/// <summary>
	/// Unique identifier for the game type.
	/// </summary>
	public Guid Id { get; set; } = Guid.CreateVersion7();

	/// <summary>
	/// Display name of the game variant (e.g., "Texas Hold'em", "Seven Card Stud", "Baseball").
	/// </summary>
	public required string Name { get; set; }

	/// <summary>
	/// Detailed description of the game variant and its rules.
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Machine-readable code for the game variant (e.g., "HOLDEM", "STUD7", "BASEBALL").
	/// Used for programmatic identification and routing.
	/// </summary>
	public required string Code { get; set; }

	/// <summary>
	/// The betting structure used by this game variant.
	/// </summary>
	public BettingStructure BettingStructure { get; set; }

	/// <summary>
	/// Minimum number of players required to start a game.
	/// </summary>
	public int MinPlayers { get; set; }

	/// <summary>
	/// Maximum number of players allowed in a game.
	/// </summary>
	public int MaxPlayers { get; set; }

	/// <summary>
	/// Number of hole (face-down) cards dealt to each player initially.
	/// </summary>
	public int InitialHoleCards { get; set; }

	/// <summary>
	/// Number of board (face-up) cards dealt to each player initially (for stud variants).
	/// </summary>
	public int InitialBoardCards { get; set; }

	/// <summary>
	/// Maximum number of community cards in this variant (for Hold'em/Omaha style games).
	/// </summary>
	public int MaxCommunityCards { get; set; }

	/// <summary>
	/// Maximum total cards a player can have (including initial deal and draws/extra cards).
	/// </summary>
	public int MaxPlayerCards { get; set; }

	/// <summary>
	/// Indicates whether this variant supports a draw phase.
	/// </summary>
	public bool HasDrawPhase { get; set; }

	/// <summary>
	/// Maximum number of cards that can be discarded in a draw phase.
	/// </summary>
	public int MaxDiscards { get; set; }

	/// <summary>
	/// The wild card rule type for this variant.
	/// </summary>
	public WildCardRule WildCardRule { get; set; }

	/// <summary>
	/// JSON-serialized variant-specific settings that don't fit the standard schema.
	/// Examples: buy-card price for Baseball, king-required for Kings and Lows.
	/// </summary>
	/// <remarks>
	/// This property enables extensibility for game-specific rules without schema changes.
	/// The JSON structure varies by game variant.
	/// </remarks>
	public string? VariantSettings { get; set; }

	/// <summary>
	/// Indicates whether this game type is currently active and available for new games.
	/// </summary>
	public bool IsActive { get; set; } = true;

	/// <summary>
	/// The date and time when this game type was created.
	/// </summary>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>
	/// The date and time when this game type was last updated.
	/// </summary>
	public DateTimeOffset UpdatedAt { get; set; }

	/// <summary>
	/// Navigation property for all games of this type.
	/// </summary>
	public ICollection<Game> Games { get; set; } = [];
}

/// <summary>
/// Defines the betting structure used by a poker game variant.
/// </summary>
public enum BettingStructure
{
	/// <summary>
	/// Uses ante bets only (e.g., Five Card Draw, Kings and Lows).
	/// </summary>
	Ante = 0,

	/// <summary>
	/// Uses small blind and big blind (e.g., Texas Hold'em, Omaha).
	/// </summary>
	Blinds = 1,

	/// <summary>
	/// Uses ante plus bring-in for lowest visible card (e.g., Seven Card Stud).
	/// </summary>
	AnteBringIn = 2,

	/// <summary>
	/// Uses ante only with drop-or-stay mechanics and pot matching (e.g., Kings and Lows).
	/// </summary>
	AntePotMatch = 3
}

/// <summary>
/// Defines the wild card rule type for a poker game variant.
/// </summary>
public enum WildCardRule
{
	/// <summary>
	/// No wild cards in this game.
	/// </summary>
	None = 0,

	/// <summary>
	/// Specific ranks are always wild (e.g., 3s and 9s in Baseball).
	/// </summary>
	FixedRanks = 1,

	/// <summary>
	/// Dynamic wild cards based on dealt cards (e.g., Follow the Queen).
	/// </summary>
	Dynamic = 2,

	/// <summary>
	/// Lowest card in hand is wild, plus specific ranks (e.g., Kings and Lows).
	/// </summary>
	LowestCard = 3
}