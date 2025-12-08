using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGamePlayers;

/// <summary>
/// Response containing a player's state within a specific game.
/// </summary>
public record GetGamePlayersResponse(
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
	string RowVersion
);
