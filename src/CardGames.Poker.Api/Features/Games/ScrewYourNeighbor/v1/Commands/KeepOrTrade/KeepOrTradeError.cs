namespace CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade;

public class KeepOrTradeError
{
	public required string Message { get; init; }
	public required KeepOrTradeErrorCode Code { get; init; }
}

public enum KeepOrTradeErrorCode
{
	GameNotFound,
	PlayerNotFound,
	InvalidPhase,
	InvalidDecision,
	NotPlayersTurn
}
