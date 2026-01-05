namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.CreateGame;

public class CreateGameSuccessful
{
	public required Guid GameId { get; init; }
	public required string GameTypeCode { get; init; }
	public required int PlayerCount { get; init; }
}
