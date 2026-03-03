namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ChooseDealerGame;

public record ChooseDealerGameError
{
	public Guid GameId { get; init; }
	public required string Reason { get; init; }
}
