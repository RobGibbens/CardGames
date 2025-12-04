namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when antes are collected from all players.
/// </summary>
public record AntesCollected(
	Guid GameId,
	Guid HandId,
	Dictionary<Guid, int> PlayerAntes,  // PlayerId -> Amount collected
	int TotalCollected,
	DateTime CollectedAt
);