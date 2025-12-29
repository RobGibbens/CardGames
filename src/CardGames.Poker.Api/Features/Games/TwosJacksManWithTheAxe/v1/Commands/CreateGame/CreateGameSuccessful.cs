using CardGames.Poker.Events;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Commands.CreateGame;

public record CreateGameSuccessful
{
	public Guid GameId { get; init; }
	public required string GameTypeCode { get; init; }
	public int PlayerCount { get; init; }
}