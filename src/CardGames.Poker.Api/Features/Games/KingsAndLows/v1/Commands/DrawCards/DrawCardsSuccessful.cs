namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DrawCards;

public class DrawCardsSuccessful
{
	public required Guid GameId { get; init; }
	public required Guid PlayerId { get; init; }
	public required int CardsDiscarded { get; init; }
	public required int CardsDrawn { get; init; }
	public required bool DrawPhaseComplete { get; init; }
	public string? NextPhase { get; init; }
	public Guid? NextPlayerId { get; init; }
}
