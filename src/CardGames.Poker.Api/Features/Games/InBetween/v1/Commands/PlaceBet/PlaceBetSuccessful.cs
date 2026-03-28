namespace CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.PlaceBet;

public class PlaceBetSuccessful
{
	public required Guid GameId { get; init; }
	public required Guid PlayerId { get; init; }
	public required int Amount { get; init; }
	public required string TurnResult { get; init; }
	public string? Description { get; init; }
	public string? NextPhase { get; init; }
	public int? NextPlayerSeatIndex { get; init; }
	public int PotAmount { get; init; }
}
