using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetCurrentDrawPlayer;

/// <summary>
/// Response containing the current draw player's state within a specific game during the draw phase.
/// </summary>
public record GetCurrentDrawPlayerResponse(
	Guid Id,
	Guid GameId,
	Guid PlayerId,
	string PlayerName,
	int SeatPosition,
	int ChipStack,
	int StartingChips,
	int CurrentBet,
	int TotalContributedThisHand,
	bool HasFolded,
	bool IsAllIn,
	bool IsConnected,
	bool IsSittingOut,
	bool HasDrawnThisRound,
	GamePlayerStatus Status,
	DateTimeOffset JoinedAt,
	string RowVersion,
	IReadOnlyList<DealtCard> Hand
);
