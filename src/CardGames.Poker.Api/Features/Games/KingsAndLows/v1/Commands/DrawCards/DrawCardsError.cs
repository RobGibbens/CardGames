namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DrawCards;

public class DrawCardsError
{
	public required string Message { get; init; }
	public required DrawCardsErrorCode Code { get; init; }
}

public enum DrawCardsErrorCode
{
	GameNotFound,
	PlayerNotFound,
	InvalidPhase,
	NotPlayerTurn,
	InvalidDiscardIndices,
	TooManyDiscards,
	PlayerHasAlreadyDrawn,
	InsufficientCards
}
