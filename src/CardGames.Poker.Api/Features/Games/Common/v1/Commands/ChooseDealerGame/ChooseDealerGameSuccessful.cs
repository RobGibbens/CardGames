namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ChooseDealerGame;

public record ChooseDealerGameSuccessful
{
	public Guid GameId { get; init; }
	public required string GameTypeCode { get; init; }
	public required string GameTypeName { get; init; }
	public int HandNumber { get; init; }
	public int Ante { get; init; }
	public int MinBet { get; init; }
}
