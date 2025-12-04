namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when blinds are collected (Hold'em/Omaha).
/// </summary>
public record BlindsCollected(
	Guid GameId,
	Guid HandId,
	Guid SmallBlindPlayerId,
	int SmallBlindAmount,
	Guid BigBlindPlayerId,
	int BigBlindAmount,
	DateTime CollectedAt
);