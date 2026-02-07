using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.DealHands;

public record DealHandsSuccessful
{
	public Guid GameId { get; init; }
	public required string CurrentPhase { get; init; }
	public int HandNumber { get; init; }
	public int CurrentPlayerIndex { get; init; }
	public string? CurrentPlayerName { get; init; }
	public required IReadOnlyList<PlayerDealtCards> PlayerHands { get; init; }
}

public record PlayerDealtCards
{
	public required string PlayerName { get; init; }
	public int SeatPosition { get; init; }
	public required IReadOnlyList<DealtCard> Cards { get; init; }
}

public record DealtCard
{
	public CardSuit Suit { get; init; }
	public CardSymbol Symbol { get; init; }
	public int DealOrder { get; init; }
}

public record DealHandsError
{
	public required string Message { get; init; }
	public required DealHandsErrorCode Code { get; init; }
}

public enum DealHandsErrorCode
{
	GameNotFound,
	InvalidGameState,
	InsufficientCards
}
