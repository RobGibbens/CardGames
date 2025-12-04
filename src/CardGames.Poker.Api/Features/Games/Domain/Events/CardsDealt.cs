namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when cards are dealt to players.
/// Note: Card values are stored encrypted/hidden in the event for security.
/// </summary>
public record CardsDealt(
	Guid GameId,
	Guid HandId,
	Dictionary<Guid, int> PlayerCardCounts,  // PlayerId -> Number of cards dealt
	DateTime DealtAt
);