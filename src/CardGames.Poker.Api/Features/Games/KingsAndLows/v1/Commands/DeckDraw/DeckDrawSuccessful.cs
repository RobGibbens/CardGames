namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DeckDraw;

public class DeckDrawSuccessful
{
	public required Guid GameId { get; init; }
	public required int CardsDiscarded { get; init; }
	public required int CardsDrawn { get; init; }
	public required string NextPhase { get; init; }
}
