namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;

/// <summary>
/// Represents an error when processing a draw action.
/// </summary>
public record ProcessDrawError
{
	/// <summary>
	/// The error message describing why the action failed.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// The error code for programmatic handling.
	/// </summary>
	public required ProcessDrawErrorCode Code { get; init; }
}

/// <summary>
/// Error codes for process draw action failures.
/// </summary>
public enum ProcessDrawErrorCode
{
	/// <summary>
	/// The specified game was not found.
	/// </summary>
	GameNotFound,

	/// <summary>
	/// The game is not in the draw phase.
	/// </summary>
	NotInDrawPhase,

	/// <summary>
	/// It is not the requesting player's turn to draw.
	/// </summary>
	NotPlayerTurn,

	/// <summary>
	/// Too many cards selected for discard (maximum is 3).
	/// </summary>
	TooManyDiscards,

	/// <summary>
	/// Invalid card index (must be 0-4).
	/// </summary>
	InvalidCardIndex,

	/// <summary>
	/// No eligible players to draw (all folded or all-in).
	/// </summary>
	NoEligiblePlayers,

	/// <summary>
	/// Not enough cards remaining in the persisted deck to fulfill the draw request.
	/// </summary>
	InsufficientDeckCards
}
