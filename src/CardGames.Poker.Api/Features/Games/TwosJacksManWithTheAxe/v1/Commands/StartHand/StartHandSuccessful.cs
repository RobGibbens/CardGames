namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.StartHand;

/// <summary>
/// Represents a successful start of a new hand in a Five Card Draw game.
/// </summary>
public record StartHandSuccessful
{
	/// <summary>
	/// The unique identifier of the game.
	/// </summary>
	public Guid GameId { get; init; }

	/// <summary>
	/// The hand number that was started.
	/// </summary>
	public int HandNumber { get; init; }

	/// <summary>
	/// The current phase of the game after starting the hand.
	/// </summary>
	public required string CurrentPhase { get; init; }

	/// <summary>
	/// The number of active players in the hand.
	/// </summary>
	public int ActivePlayerCount { get; init; }
}

/// <summary>
/// Represents an error when starting a new hand.
/// </summary>
public record StartHandError
{
	/// <summary>
	/// The error message describing why the hand could not be started.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// The error code for programmatic handling.
	/// </summary>
	public required StartHandErrorCode Code { get; init; }
}

/// <summary>
/// Error codes for start hand failures.
/// </summary>
public enum StartHandErrorCode
{
	/// <summary>
	/// The specified game was not found.
	/// </summary>
	GameNotFound,

	/// <summary>
	/// The game is not in a valid state to start a new hand.
	/// </summary>
	InvalidGameState,

	/// <summary>
	/// Not enough players with chips to continue.
	/// </summary>
	NotEnoughPlayers
}
