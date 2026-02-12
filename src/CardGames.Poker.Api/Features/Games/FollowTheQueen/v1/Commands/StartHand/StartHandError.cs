namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.StartHand;

/// <summary>
/// Represents an error that occurred while starting a hand.
/// </summary>
public record StartHandError
{
	/// <summary>
	/// The error message.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// The error code.
	/// </summary>
	public required StartHandErrorCode Code { get; init; }
}

/// <summary>
/// Error codes for start hand operation.
/// </summary>
public enum StartHandErrorCode
{
	GameNotFound,
	InvalidGameState,
	NotEnoughPlayers
}
