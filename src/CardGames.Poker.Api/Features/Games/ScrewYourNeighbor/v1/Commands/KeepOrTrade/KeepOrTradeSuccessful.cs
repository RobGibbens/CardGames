namespace CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade;

public class KeepOrTradeSuccessful
{
	public required Guid GameId { get; init; }
	public required Guid PlayerId { get; init; }
	public required string Decision { get; init; }
	public bool DidTrade { get; init; }
	public bool WasBlocked { get; init; }
	public string? NextPhase { get; init; }
	public int? NextPlayerSeatIndex { get; init; }
}
