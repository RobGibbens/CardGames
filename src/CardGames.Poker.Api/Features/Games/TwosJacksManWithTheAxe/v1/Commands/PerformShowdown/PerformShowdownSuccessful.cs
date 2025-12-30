using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.PerformShowdown;

/// <summary>
/// Represents a successful showdown result.
/// </summary>
public record PerformShowdownSuccessful
{
	/// <summary>
	/// The unique identifier of the game.
	/// </summary>
	public Guid GameId { get; init; }

	/// <summary>
	/// Indicates whether the hand was won by all opponents folding (no showdown required).
	/// </summary>
	public bool WonByFold { get; init; }

	/// <summary>
	/// The current phase of the game after the showdown.
	/// </summary>
	public required string CurrentPhase { get; init; }

	/// <summary>
	/// The chip payouts awarded to each winning player.
	/// Keys are player names; values are the chip amounts won.
	/// Multiple entries indicate a split pot scenario.
	/// </summary>
	public required Dictionary<string, int> Payouts { get; init; }

	/// <summary>
	/// The evaluated hands for all players who participated in the showdown.
	/// </summary>
	public required List<ShowdownPlayerHand> PlayerHands { get; init; }
}

/// <summary>
/// Represents a player's hand at showdown.
/// </summary>
public record ShowdownPlayerHand
{
	/// <summary>
	/// The player's name.
	/// </summary>
	public required string PlayerName { get; init; }

	/// <summary>
	/// The player's first name.
	/// </summary>
	public string? PlayerFirstName { get; init; }

	/// <summary>
	/// The cards the player held.
	/// </summary>
	public required List<ShowdownCard> Cards { get; init; }

	/// <summary>
	/// The evaluated hand type (e.g., "Flush", "FullHouse", "HighCard").
	/// Null if the player won by fold without showing cards.
	/// </summary>
	public string? HandType { get; init; }

	/// <summary>
	/// A detailed, human-friendly description of the evaluated hand (e.g., "Ace high", "Pair of Aces", "Straight to the Queen").
	/// Null if the player won by fold without showing cards.
	/// </summary>
	public string? HandDescription { get; init; }

	/// <summary>
	/// The hand strength value used for comparing hands.
	/// Higher values indicate stronger hands.
	/// </summary>
	public long? HandStrength { get; init; }

	/// <summary>
	/// Indicates whether this player is a winner.
	/// </summary>
	public bool IsWinner { get; init; }

	/// <summary>
	/// The amount won by this player, if any.
	/// </summary>
	public int AmountWon { get; init; }
}

/// <summary>
/// Represents a card shown at showdown.
/// </summary>
public record ShowdownCard
{
	/// <summary>
	/// The card suit.
	/// </summary>
	public CardSuit Suit { get; init; }

	/// <summary>
	/// The card symbol/rank.
	/// </summary>
	public CardSymbol Symbol { get; init; }
}

/// <summary>
/// Represents an error that occurred during the showdown.
/// </summary>
public record PerformShowdownError
{
	/// <summary>
	/// The error message describing what went wrong.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// The error code categorizing the type of error.
	/// </summary>
	public required PerformShowdownErrorCode Code { get; init; }
}

/// <summary>
/// Error codes for showdown failures.
/// </summary>
public enum PerformShowdownErrorCode
{
	/// <summary>
	/// The game was not found.
	/// </summary>
	GameNotFound,

	/// <summary>
	/// The game is not in the showdown phase.
	/// </summary>
	InvalidGameState
}

