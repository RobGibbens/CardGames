namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when a new hand is started in a game.
/// </summary>
public record HandStarted(
	Guid GameId,
	Guid HandId,
	int HandNumber,
	int DealerPosition,
	DateTime StartedAt
);
