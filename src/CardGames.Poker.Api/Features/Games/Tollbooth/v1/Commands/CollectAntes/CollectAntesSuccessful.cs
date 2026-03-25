namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.CollectAntes;

public record CollectAntesSuccessful
{
	public required Guid GameId { get; init; }
	public required int TotalAntesCollected { get; init; }
	public required string CurrentPhase { get; init; }
	public required List<AnteContribution> AnteContributions { get; init; }
}

public record AnteContribution
{
	public required string PlayerName { get; init; }
	public required int Amount { get; init; }
	public required int RemainingChips { get; init; }
	public required bool WentAllIn { get; init; }
}
