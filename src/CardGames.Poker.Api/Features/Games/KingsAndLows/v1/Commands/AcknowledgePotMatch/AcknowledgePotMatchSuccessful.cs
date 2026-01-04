namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.AcknowledgePotMatch;

public class AcknowledgePotMatchSuccessful
{
	public required Guid GameId { get; init; }
	public required int TotalMatched { get; init; }
	public required int NewPotAmount { get; init; }
	public required string NextPhase { get; init; }
	public required Dictionary<string, int> MatchAmounts { get; init; }
}
