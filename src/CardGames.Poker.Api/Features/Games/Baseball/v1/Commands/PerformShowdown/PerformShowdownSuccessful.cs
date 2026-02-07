using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.PerformShowdown;

public record PerformShowdownSuccessful
{
	public Guid GameId { get; init; }
	public bool WonByFold { get; init; }
	public required string CurrentPhase { get; init; }
	public required Dictionary<string, int> Payouts { get; init; }
	public required List<ShowdownPlayerHand> PlayerHands { get; init; }
}

public record ShowdownPlayerHand
{
	public required string PlayerName { get; init; }
	public string? PlayerFirstName { get; init; }
	public required List<ShowdownCard> Cards { get; init; }
	public string? HandType { get; init; }
	public string? HandDescription { get; init; }
	public long? HandStrength { get; init; }
	public bool IsWinner { get; init; }
	public int AmountWon { get; init; }
	public List<int>? BestCardIndexes { get; init; }
	public List<int>? WildCardIndexes { get; init; }
}

public record ShowdownCard
{
	public CardSuit Suit { get; init; }
	public CardSymbol Symbol { get; init; }
}

public record PerformShowdownError
{
	public required string Message { get; init; }
	public required PerformShowdownErrorCode Code { get; init; }
}

public enum PerformShowdownErrorCode
{
	GameNotFound,
	InvalidGameState
}
