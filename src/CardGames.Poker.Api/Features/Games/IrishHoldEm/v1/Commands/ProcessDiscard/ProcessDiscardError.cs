namespace CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;

/// <summary>
/// Represents an error when processing a discard action.
/// </summary>
public record ProcessDiscardError
{
	/// <summary>
	/// The error message describing why the action failed.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// The error code for programmatic handling.
	/// </summary>
	public required ProcessDiscardErrorCode Code { get; init; }
}

/// <summary>
/// Error codes for process discard action failures.
/// </summary>
public enum ProcessDiscardErrorCode
{
	/// <summary>
	/// The specified game was not found.
	/// </summary>
	GameNotFound,

	/// <summary>
	/// The game is not in the discard phase.
	/// </summary>
	NotInDiscardPhase,

	/// <summary>
	/// It is not the requesting player's turn to discard.
	/// </summary>
	NotPlayerTurn,

	/// <summary>
	/// Must discard exactly 2 cards.
	/// </summary>
	InvalidDiscardCount,

	/// <summary>
	/// Invalid card index (must be 0-3).
	/// </summary>
	InvalidCardIndex,

	/// <summary>
	/// No eligible players to discard (all folded or all-in).
	/// </summary>
	NoEligiblePlayers,

	/// <summary>
	/// Player has already discarded this round.
	/// </summary>
	AlreadyDiscarded,

	/// <summary>
	/// Player does not have enough cards to discard.
	/// </summary>
	InsufficientCards
}
