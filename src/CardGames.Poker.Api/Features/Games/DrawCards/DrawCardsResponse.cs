namespace CardGames.Poker.Api.Features.Games.DrawCards;

/// <summary>
/// Response returned after drawing cards.
/// </summary>
public record DrawCardsResponse(
	bool Success,
	int CardsDiscarded,
	List<string> NewCards,
	List<string> NewHand,
	bool DrawPhaseComplete,
	Guid? NextPlayerToAct,
	string CurrentPhase
);
