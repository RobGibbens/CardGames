namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.ChooseCard;

public record ChooseCardError
{
	public required string Message { get; init; }

	public required ChooseCardErrorCode Code { get; init; }
}

public enum ChooseCardErrorCode
{
	GameNotFound,
	NotInTollboothPhase,
	NotPlayerTurn,
	NoEligiblePlayers,
	AlreadyChosen,
	CannotAfford,
	InvalidChoice
}
