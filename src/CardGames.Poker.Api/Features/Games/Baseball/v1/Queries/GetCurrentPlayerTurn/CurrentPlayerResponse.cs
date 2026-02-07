using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.DealHands;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Queries.GetCurrentPlayerTurn;

public record CurrentPlayerResponse(
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
