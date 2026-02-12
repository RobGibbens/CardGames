namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.CollectAntes;

/// <summary>
/// Represents an error that occurred while collecting antes.
/// </summary>
public record CollectAntesError
{
	/// <summary>
	/// The error message.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// The error code.
	/// </summary>
	public required CollectAntesErrorCode Code { get; init; }
}

/// <summary>
/// Error codes for collect antes operation.
/// </summary>
public enum CollectAntesErrorCode
{
	GameNotFound,
	InvalidGameState
}
