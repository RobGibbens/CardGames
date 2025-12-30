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
	/// The chip payouts awarded to each winning player (total of sevens pool + high hand pool).
	/// Keys are player names; values are the chip amounts won.
	/// Multiple entries indicate a split pot scenario.
	/// </summary>
	public required Dictionary<string, int> Payouts { get; init; }

	/// <summary>
	/// The evaluated hands for all players who participated in the showdown.
	/// </summary>
	public required List<ShowdownPlayerHand> PlayerHands { get; init; }

	/// <summary>
	/// Player names who won the sevens pool (had a natural pair of 7s).
	/// Empty if no players qualified for the sevens pool.
	/// </summary>
	public List<string> SevensWinners { get; init; } = [];

	/// <summary>
	/// Player names who won the high hand pool.
	/// </summary>
	public List<string> HighHandWinners { get; init; } = [];

	/// <summary>
	/// Payouts from the sevens pool per player.
	/// Keys are player names; values are chip amounts won from the sevens pool.
	/// </summary>
	public Dictionary<string, int> SevensPayouts { get; init; } = [];

	/// <summary>
	/// Payouts from the high hand pool per player.
	/// Keys are player names; values are chip amounts won from the high hand pool.
	/// </summary>
	public Dictionary<string, int> HighHandPayouts { get; init; } = [];

	/// <summary>
	/// Whether the sevens pool was rolled into the high hand pool
	/// because no players had a natural pair of 7s.
	/// </summary>
	public bool SevensPoolRolledOver { get; init; }
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
	/// Indicates whether this player is a winner (either sevens or high hand).
	/// </summary>
	public bool IsWinner { get; init; }

	/// <summary>
	/// Indicates whether this player won the sevens pool (had a natural pair of 7s).
	/// </summary>
	public bool IsSevensWinner { get; init; }

	/// <summary>
	/// Indicates whether this player won the high hand pool.
	/// </summary>
	public bool IsHighHandWinner { get; init; }

	/// <summary>
	/// The total amount won by this player (sevens + high hand).
	/// </summary>
	public int AmountWon { get; init; }

	/// <summary>
	/// The amount won from the sevens pool.
	/// </summary>
	public int SevensAmountWon { get; init; }

	/// <summary>
	/// The amount won from the high hand pool.
	/// </summary>
	public int HighHandAmountWon { get; init; }

	/// <summary>
	/// The zero-based indices of cards in the hand that are wild.
	/// Used by the UI to display wild card indicators.
	/// </summary>
	public List<int>? WildCardIndexes { get; init; }
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

