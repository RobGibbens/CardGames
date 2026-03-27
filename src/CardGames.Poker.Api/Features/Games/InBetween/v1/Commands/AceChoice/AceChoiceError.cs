namespace CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.AceChoice;

public class AceChoiceError
{
	public required string Message { get; init; }
	public required AceChoiceErrorCode Code { get; init; }
}

public enum AceChoiceErrorCode
{
	GameNotFound,
	PlayerNotFound,
	InvalidPhase,
	NotPlayersTurn,
	AceChoiceNotRequired
}
