using CardGames.Poker.Betting;

namespace CardGames.Poker.Api.Features.Games.PlaceAction;

/// <summary>
/// Request to place a betting action.
/// </summary>
public record PlaceActionRequest(
	Guid PlayerId,
	BettingActionType ActionType,
	int Amount = 0
);
