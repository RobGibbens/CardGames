namespace CardGames.Poker.Api.Features.Games.PairPressure.v1.Commands.StartHand;

public record StartHandSuccessful
{
	public required Guid GameId { get; init; }
	public required int HandNumber { get; init; }
	public required string CurrentPhase { get; init; }
	public required int ActivePlayerCount { get; init; }
}