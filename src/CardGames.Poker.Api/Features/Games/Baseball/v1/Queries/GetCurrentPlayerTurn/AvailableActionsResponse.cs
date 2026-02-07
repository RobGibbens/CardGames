namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Queries.GetCurrentPlayerTurn;

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
	int MinRaise);
