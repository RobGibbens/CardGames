namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.StartHand;

/// <summary>
/// Represents a successful start hand operation.
/// </summary>
public record StartHandSuccessful
{
	/// <summary>
	/// The unique identifier of the game.
	/// </summary>
	public required Guid GameId { get; init; }

	/// <summary>
	/// The current hand number.
	/// </summary>
	public required int HandNumber { get; init; }

	/// <summary>
	/// The current phase of the game.
	/// </summary>
	public required string CurrentPhase { get; init; }

	/// <summary>
	/// The number of active players in the hand.
	/// </summary>
	public required int ActivePlayerCount { get; init; }
}
