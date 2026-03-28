namespace CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.PlaceBet;

public class PlaceBetError
{
	public required string Message { get; init; }
	public required PlaceBetErrorCode Code { get; init; }
}

public enum PlaceBetErrorCode
{
	GameNotFound,
	PlayerNotFound,
	InvalidPhase,
	NotPlayersTurn,
	InvalidBetAmount,
	BetExceedsPot,
	BetExceedsChips,
	FullPotNotAllowedFirstOrbit,
	AceChoiceRequired
}
