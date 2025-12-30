namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.CreateGame;

public record CreateGameConflict
{
	public Guid GameId { get; init; }
	public required string Reason { get; init; }
}
