namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.PerformShowdown;

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
