namespace CardGames.Poker.Api.Features.Games.GetCurrentHand;

/// <summary>
/// Response containing the current hand state.
/// </summary>
public record GetCurrentHandResponse(
	Guid HandId,
	int HandNumber,
	string Phase,
	int Pot,
	Guid? CurrentPlayerToAct,
	int CurrentBet,
	int DealerPosition,
	List<HandPlayerStateResponse> Players
);

/// <summary>
/// State of a player in the current hand.
/// </summary>
public record HandPlayerStateResponse(
	Guid PlayerId,
	string Name,
	int ChipStack,
	int CurrentBet,
	string Status,
	int CardCount
);
