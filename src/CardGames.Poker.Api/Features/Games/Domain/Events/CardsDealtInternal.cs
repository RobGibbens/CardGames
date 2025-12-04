namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Internal event for tracking actual card values (not exposed via API).
/// </summary>
public record CardsDealtInternal(
	Guid GameId,
	Guid HandId,
	Dictionary<Guid, List<string>> PlayerCards,  // PlayerId -> Card strings (e.g., "Ah", "Kd")
	DateTime DealtAt
);