namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.DeleteGame;

/// <summary>
/// Error codes for delete game operation.
/// </summary>
public enum DeleteGameErrorCode
{
	/// <summary>
	/// The game was not found.
	/// </summary>
	GameNotFound,

	/// <summary>
	/// The user is not authorized to delete the game.
	/// </summary>
	NotAuthorized,

	/// <summary>
	/// The game has already been deleted.
	/// </summary>
	AlreadyDeleted
}

/// <summary>
/// Result when a game deletion fails.
/// </summary>
public record DeleteGameError
{
	/// <summary>
	/// The error code.
	/// </summary>
	public required DeleteGameErrorCode Code { get; init; }

	/// <summary>
	/// The error message.
	/// </summary>
	public required string Message { get; init; }
}

