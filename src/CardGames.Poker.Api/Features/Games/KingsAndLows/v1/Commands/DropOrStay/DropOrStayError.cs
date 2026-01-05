namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;

public class DropOrStayError
{
	public required string Message { get; init; }
	public required DropOrStayErrorCode Code { get; init; }
}

public enum DropOrStayErrorCode
{
	GameNotFound,
	PlayerNotFound,
	InvalidPhase,
	InvalidDecision,
	AlreadyDecided
}
