namespace CardGames.Poker.Api.Features.Games.PlaceAction;

/// <summary>
/// Response returned after processing a betting action.
/// </summary>
public record PlaceActionResponse(
	bool Success,
	string ActionDescription,
	int NewPot,
	Guid? NextPlayerToAct,
	bool RoundComplete,
	bool PhaseAdvanced,
	string CurrentPhase,
	string? ErrorMessage = null
);
