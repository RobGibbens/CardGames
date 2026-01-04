namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.StartHand;

public class StartHandError
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
