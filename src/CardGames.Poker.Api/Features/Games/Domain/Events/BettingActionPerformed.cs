using CardGames.Poker.Betting;

namespace CardGames.Poker.Api.Features.Games.Domain.Events;

/// <summary>
/// Domain event raised when a betting action is performed.
/// </summary>
public record BettingActionPerformed(
	Guid GameId,
	Guid HandId,
	Guid PlayerId,
	BettingActionType ActionType,
	int Amount,
	int NewPot,
	int PlayerChipStack,
	bool RoundComplete,
	string? NewPhase,
	DateTime PerformedAt
);
