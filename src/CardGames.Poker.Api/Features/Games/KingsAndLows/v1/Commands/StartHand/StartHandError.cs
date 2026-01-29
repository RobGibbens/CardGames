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
	NotEnoughPlayers,
	/// <summary>
	/// The game is paused because one or more players cannot cover the current pot.
	/// Players have 2 minutes to add chips before being auto-dropped.
	/// </summary>
	PausedForChipCheck
}
