namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;

public class DropOrStaySuccessful
{
	public required Guid GameId { get; init; }
	public required Guid PlayerId { get; init; }
	public required string Decision { get; init; }
	public required bool AllPlayersDecided { get; init; }
	public required int StayingCount { get; init; }
	public required int DroppedCount { get; init; }
	public string? NextPhase { get; init; }
}
