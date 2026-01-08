using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands;

/// <summary>
/// Represents a successful deal of cards to all players.
/// </summary>
public record DealHandsSuccessful
{
	/// <summary>
	/// The unique identifier of the game.
	/// </summary>
	public Guid GameId { get; init; }

	/// <summary>
	/// The current phase of the game after dealing.
	/// </summary>
	public required string CurrentPhase { get; init; }

	/// <summary>
	/// The current hand number being played.
	/// </summary>
	public int HandNumber { get; init; }

	/// <summary>
	/// The index of the current player who must act.
	/// </summary>
	public int CurrentPlayerIndex { get; init; }

	/// <summary>
	/// The name of the current player who must act.
	/// </summary>
	public string? CurrentPlayerName { get; init; }

	/// <summary>
	/// The cards dealt to each player.
	/// </summary>
	public required IReadOnlyList<PlayerDealtCards> PlayerHands { get; init; }
}

/// <summary>
/// Represents the cards dealt to a player.
/// </summary>
public record PlayerDealtCards
{
	/// <summary>
	/// The name of the player who received the cards.
	/// </summary>
	public required string PlayerName { get; init; }

	/// <summary>
	/// The seat position of the player.
	/// </summary>
	public int SeatPosition { get; init; }

	/// <summary>
	/// The cards dealt to this player.
	/// </summary>
	public required IReadOnlyList<DealtCard> Cards { get; init; }
}

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
