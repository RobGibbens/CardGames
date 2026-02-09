namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.CollectAntes;

public record CollectAntesSuccessful
{
	public Guid GameId { get; init; }
	public int TotalAntesCollected { get; init; }
	public required string CurrentPhase { get; init; }
	public required List<AnteContribution> AnteContributions { get; init; }
}

public record AnteContribution
{
	public required string PlayerName { get; init; }
	public int Amount { get; init; }
	public int RemainingChips { get; init; }
	public bool WentAllIn { get; init; }
}

public record CollectAntesError
{
	public required string Message { get; init; }
	public required CollectAntesErrorCode Code { get; init; }
}

public enum CollectAntesErrorCode
{
	GameNotFound,
	InvalidGameState
}
