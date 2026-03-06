namespace CardGames.Poker.Api.Data.Entities;

/// <summary>
/// Records which game type was played for each hand in a Dealer's Choice game.
/// </summary>
public class DealersChoiceHandLog
{
	public Guid Id { get; set; } = Guid.CreateVersion7();

	public Guid GameId { get; set; }
	public Game Game { get; set; } = null!;

	/// <summary>
	/// 1-based hand number within the game session.
	/// </summary>
	public int HandNumber { get; set; }

	/// <summary>
	/// The game type code chosen (e.g., "FIVECARDDRAW", "SEVENCARDSTUD").
	/// </summary>
	public required string GameTypeCode { get; set; }

	/// <summary>
	/// Display name of the game type chosen.
	/// </summary>
	public required string GameTypeName { get; set; }

	/// <summary>
	/// The player ID who made the choice.
	/// </summary>
	public Guid DealerPlayerId { get; set; }

	/// <summary>
	/// The seat position of the dealer who chose.
	/// </summary>
	public int DealerSeatPosition { get; set; }

	/// <summary>
	/// Ante chosen for this hand.
	/// </summary>
	public int Ante { get; set; }

	/// <summary>
	/// Min bet chosen for this hand.
	/// </summary>
	public int MinBet { get; set; }

	/// <summary>
	/// Small blind chosen for this hand (blind-based games like Hold 'Em).
	/// </summary>
	public int? SmallBlind { get; set; }

	/// <summary>
	/// Big blind chosen for this hand (blind-based games like Hold 'Em).
	/// </summary>
	public int? BigBlind { get; set; }

	public DateTimeOffset ChosenAtUtc { get; set; }
}
