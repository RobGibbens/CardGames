namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.AcknowledgePotMatch;

public class AcknowledgePotMatchError
{
	public required string Message { get; init; }
	public required AcknowledgePotMatchErrorCode Code { get; init; }
}

public enum AcknowledgePotMatchErrorCode
{
	GameNotFound,
	InvalidPhase,
	NoPotToMatch
}
