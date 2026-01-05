namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.StartHand;

public class StartHandSuccessful
{
	public required Guid GameId { get; init; }
	public required int HandNumber { get; init; }
	public required string CurrentPhase { get; init; }
	public required int ActivePlayerCount { get; init; }
}
