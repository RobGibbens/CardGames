using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.DealHands;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Queries.GetCurrentPlayerTurn;

/// <summary>
/// Response containing the current player's state within a specific game.
/// </summary>
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
