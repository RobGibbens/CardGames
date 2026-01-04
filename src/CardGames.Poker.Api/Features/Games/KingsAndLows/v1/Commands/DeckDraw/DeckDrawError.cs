namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DeckDraw;

public class DeckDrawError
{
	public required string Message { get; init; }
	public required DeckDrawErrorCode Code { get; init; }
}

public enum DeckDrawErrorCode
{
	GameNotFound,
	PlayerNotFound,
	InvalidPhase,
	DeckAlreadyDrawn,
	InvalidDiscardIndices,
	TooManyDiscards,
	NotPlayerVsDeckScenario
}
