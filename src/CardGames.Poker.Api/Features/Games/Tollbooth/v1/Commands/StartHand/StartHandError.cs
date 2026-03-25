namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.StartHand;

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
