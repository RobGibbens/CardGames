namespace CardGames.Poker.Api.Features.Games.HoldTheBaseball.v1.Commands.StartHand;

/// <summary>
/// Represents a successful start of a new hand in a Hold the Baseball game.
/// </summary>
public record StartHandSuccessful
{
	public Guid GameId { get; init; }
	public int HandNumber { get; init; }
	public required string CurrentPhase { get; init; }
	public int ActivePlayerCount { get; init; }
}

/// <summary>
/// Represents an error when starting a new hand.
/// </summary>
public record StartHandError
{
	public required string Message { get; init; }
	public required StartHandErrorCode Code { get; init; }
}

public enum StartHandErrorCode
{
	GameNotFound,
	InvalidGameState,
	NotEnoughPlayers
}
