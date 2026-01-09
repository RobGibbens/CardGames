using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Models;

/// <summary>
/// Represents a single dealt card.
/// </summary>
public record DealtCard
{
	/// <summary>
	/// The suit of the card.
	/// </summary>
	public CardSuit Suit { get; init; }

	/// <summary>
	/// The symbol/rank of the card.
	/// </summary>
	public CardSymbol Symbol { get; init; }

	/// <summary>
	/// The order in which this card was dealt to the player.
	/// </summary>
	public int DealOrder { get; init; }
}

/// <summary>
/// Represents an error when dealing hands.
/// </summary>
public record DealHandsError
{
	/// <summary>
	/// The error message describing why hands could not be dealt.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// The error code for programmatic handling.
	/// </summary>
	public required DealHandsErrorCode Code { get; init; }
}

/// <summary>
/// Error codes for deal hands failures.
/// </summary>
public enum DealHandsErrorCode
{
	/// <summary>
	/// The specified game was not found.
	/// </summary>
	GameNotFound,

	/// <summary>
	/// The game is not in a valid state to deal hands.
	/// </summary>
	InvalidGameState,

	/// <summary>
	/// Not enough cards remaining in the deck to deal to all players.
	/// </summary>
	InsufficientCards
}