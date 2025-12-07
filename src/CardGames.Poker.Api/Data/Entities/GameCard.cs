namespace CardGames.Poker.Api.Data.Entities;

/// <summary>
/// Represents a card in a game with its position, visibility, and ownership.
/// </summary>
/// <remarks>
/// <para>
/// GameCard tracks every card dealt in a game session, including:
/// </para>
/// <list type="bullet">
///   <item><description>The card identity (suit and symbol/rank)</description></item>
///   <item><description>Card location (hole, board, community, deck)</description></item>
///   <item><description>Order dealt and visibility status</description></item>
///   <item><description>Owner (player or community)</description></item>
///   <item><description>Special status (wild, discarded, etc.)</description></item>
/// </list>
/// <para>
/// This design supports all poker variants:
/// </para>
/// <list type="bullet">
///   <item><description>Hold'em/Omaha: Hole cards per player + community cards</description></item>
///   <item><description>Stud games: Hole cards (face-down) + board cards (face-up) per player</description></item>
///   <item><description>Draw games: Hand cards with discard/draw tracking</description></item>
/// </list>
/// </remarks>
public class GameCard : EntityWithRowVersion
{
	/// <summary>
	/// Unique identifier for this card instance.
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
	/// Foreign key to the player who owns this card (null for community cards).
	/// </summary>
	public Guid? GamePlayerId { get; set; }

	/// <summary>
	/// Navigation property to the owning player.
	/// </summary>
	public GamePlayer? GamePlayer { get; set; }

	/// <summary>
	/// The hand number when this card was dealt.
	/// </summary>
	public int HandNumber { get; set; }

	/// <summary>
	/// The suit of the card.
	/// </summary>
	public CardSuit Suit { get; set; }

	/// <summary>
	/// The symbol/rank of the card.
	/// </summary>
	public CardSymbol Symbol { get; set; }

	/// <summary>
	/// The location type of this card.
	/// </summary>
	public CardLocation Location { get; set; }

	/// <summary>
	/// The order this card was dealt (1-based within its location type).
	/// </summary>
	/// <remarks>
	/// For community cards: 1-5 for flop/turn/river
	/// For hole cards: 1-N for the order dealt to that player
	/// For board cards: 1-N for the order dealt face-up
	/// </remarks>
	public int DealOrder { get; set; }

	/// <summary>
	/// The street/phase when this card was dealt.
	/// </summary>
	/// <remarks>
	/// Examples: "PreFlop", "Flop", "Turn", "River", "ThirdStreet", "FourthStreet", "Draw1", etc.
	/// </remarks>
	public string? DealtAtPhase { get; set; }

	/// <summary>
	/// Indicates whether this card is visible to all players.
	/// </summary>
	/// <remarks>
	/// True for: community cards, board cards (face-up in stud)
	/// False for: hole cards, face-down cards
	/// </remarks>
	public bool IsVisible { get; set; }

	/// <summary>
	/// Indicates whether this card is currently wild in the game context.
	/// </summary>
	public bool IsWild { get; set; }

	/// <summary>
	/// Indicates whether this card has been discarded (for draw games).
	/// </summary>
	public bool IsDiscarded { get; set; }

	/// <summary>
	/// The draw round when this card was discarded (null if not discarded).
	/// </summary>
	public int? DiscardedAtDrawRound { get; set; }

	/// <summary>
	/// Indicates whether this card was received via a draw (not initial deal).
	/// </summary>
	public bool IsDrawnCard { get; set; }

	/// <summary>
	/// The draw round when this card was received (null if from initial deal).
	/// </summary>
	public int? DrawnAtRound { get; set; }

	/// <summary>
	/// Indicates whether this is a buy-card (Baseball variant).
	/// </summary>
	public bool IsBuyCard { get; set; }

	/// <summary>
	/// The date and time when this card was dealt.
	/// </summary>
	public DateTimeOffset DealtAt { get; set; }
}

/// <summary>
/// The suit of a playing card.
/// </summary>
/// <remarks>
/// Values match the CardGames.Core.French.Cards.Suit enum for compatibility.
/// </remarks>
public enum CardSuit
{
	/// <summary>Hearts suit (red)</summary>
	Hearts = 0,
	
	/// <summary>Diamonds suit (red)</summary>
	Diamonds = 1,
	
	/// <summary>Spades suit (black)</summary>
	Spades = 2,
	
	/// <summary>Clubs suit (black)</summary>
	Clubs = 3
}

/// <summary>
/// The symbol/rank of a playing card.
/// </summary>
/// <remarks>
/// Values match the CardGames.Core.French.Cards.Symbol enum for compatibility.
/// </remarks>
public enum CardSymbol
{
	/// <summary>Deuce (2)</summary>
	Deuce = 2,
	
	/// <summary>Three (3)</summary>
	Three = 3,
	
	/// <summary>Four (4)</summary>
	Four = 4,
	
	/// <summary>Five (5)</summary>
	Five = 5,
	
	/// <summary>Six (6)</summary>
	Six = 6,
	
	/// <summary>Seven (7)</summary>
	Seven = 7,
	
	/// <summary>Eight (8)</summary>
	Eight = 8,
	
	/// <summary>Nine (9)</summary>
	Nine = 9,
	
	/// <summary>Ten (10)</summary>
	Ten = 10,
	
	/// <summary>Jack (J)</summary>
	Jack = 11,
	
	/// <summary>Queen (Q)</summary>
	Queen = 12,
	
	/// <summary>King (K)</summary>
	King = 13,
	
	/// <summary>Ace (A)</summary>
	Ace = 14
}

/// <summary>
/// The location type of a card in the game.
/// </summary>
public enum CardLocation
{
	/// <summary>
	/// Card is in the deck (not yet dealt).
	/// </summary>
	Deck = 0,

	/// <summary>
	/// Card is a hole card (face-down, owned by a player).
	/// </summary>
	Hole = 1,

	/// <summary>
	/// Card is a board card (face-up, owned by a player, for stud games).
	/// </summary>
	Board = 2,

	/// <summary>
	/// Card is a community card (shared by all players).
	/// </summary>
	Community = 3,

	/// <summary>
	/// Card is in a player's hand (for draw games before dealing style).
	/// </summary>
	Hand = 4,

	/// <summary>
	/// Card has been discarded.
	/// </summary>
	Discarded = 5,

	/// <summary>
	/// Card was mucked (not shown at showdown).
	/// </summary>
	Mucked = 6
}
