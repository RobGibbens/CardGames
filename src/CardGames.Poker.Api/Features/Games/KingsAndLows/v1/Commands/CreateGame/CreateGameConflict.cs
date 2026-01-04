namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.CreateGame;

public class CreateGameConflict
{
	public required Guid GameId { get; init; }
	public required string Reason { get; init; }
}
