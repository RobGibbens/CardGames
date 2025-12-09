namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetCurrentBettingRound;

/// <summary>
/// Response containing the current betting round information for a game.
/// </summary>
public record GetCurrentBettingRoundResponse(
Guid Id,
Guid GameId,
int HandNumber,
int RoundNumber,
string Street,
int CurrentBet,
int MinBet,
int TotalPot,
int RaiseCount,
	int MaxRaises,
	int LastRaiseAmount,
	int PlayersInHand,
	int PlayersActed,
	int CurrentActorIndex,
	int LastAggressorIndex,
	bool IsComplete,
	DateTimeOffset StartedAt,
	DateTimeOffset? CompletedAt,
	string RowVersion
);
