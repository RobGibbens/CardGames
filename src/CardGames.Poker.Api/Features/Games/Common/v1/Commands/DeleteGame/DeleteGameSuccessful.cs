namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.DeleteGame;

/// <summary>
/// Result when a game is successfully deleted.
/// </summary>
public record DeleteGameSuccessful
{
	/// <summary>
	/// The unique identifier of the deleted game.
	/// </summary>
	public required Guid GameId { get; init; }
}

