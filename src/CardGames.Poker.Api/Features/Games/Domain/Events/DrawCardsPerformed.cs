namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when a player draws cards in the draw phase.
/// </summary>
public record DrawCardsPerformed(
	Guid GameId,
	Guid HandId,
	Guid PlayerId,
	int CardsDiscarded,
	List<string> NewCards,
	bool DrawPhaseComplete,
	Guid? NextPlayerToAct,
	DateTime PerformedAt
);
