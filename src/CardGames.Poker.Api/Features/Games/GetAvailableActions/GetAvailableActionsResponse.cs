namespace CardGames.Poker.Api.Features.Games.GetAvailableActions;

/// <summary>
/// Response containing available betting actions for a player.
/// </summary>
public record GetAvailableActionsResponse(
	Guid PlayerId,
	bool IsCurrentPlayer,
	AvailableActionsDto Actions
);

/// <summary>
/// DTO for available actions matching the domain AvailableActions class.
/// </summary>
public record AvailableActionsDto(
	bool CanCheck,
	bool CanBet,
	bool CanCall,
	bool CanRaise,
	bool CanFold,
	bool CanAllIn,
	int MinBet,
	int MaxBet,
	int CallAmount,
	int MinRaise
);
