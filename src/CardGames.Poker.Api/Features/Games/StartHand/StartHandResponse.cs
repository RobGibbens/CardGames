namespace CardGames.Poker.Api.Features.Games.StartHand;

/// <summary>
/// Response returned after successfully starting a new hand.
/// </summary>
public record StartHandResponse(
	Guid HandId,
	int HandNumber,
	string Phase,
	int DealerPosition,
	int Pot,
	Guid? NextPlayerToAct
);