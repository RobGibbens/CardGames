namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.StartHand;

public record StartHandSuccessful
{
	public Guid GameId { get; init; }
	public int HandNumber { get; init; }
	public required string CurrentPhase { get; init; }
	public int ActivePlayerCount { get; init; }
}

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
