namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetCurrentPlayerTurn;

/// <summary>
/// Represents the available betting actions for the current player.
/// </summary>
public record AvailableActionsResponse(
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
