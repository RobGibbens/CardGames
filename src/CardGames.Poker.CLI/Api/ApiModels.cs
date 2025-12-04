using System;
using System.Collections.Generic;
using System.Text;

namespace CardGames.Poker.CLI.Api;

// Enums matching the API
public enum GameType
{
	FiveCardDraw,
	TexasHoldEm,
	SevenCardStud,
	Omaha,
	Baseball,
	KingsAndLows,
	FollowTheQueen
}
// Betting action type enum (matches API)
public enum BettingActionType
{
	Check,
	Bet,
	Call,
	Raise,
	Fold,
	AllIn,
	Post
}

// Hand lifecycle request/response DTOs
public record StartHandResponse(
	Guid HandId,
	int HandNumber,
	string Phase,
	int DealerPosition,
	int Pot,
	Guid? NextPlayerToAct
);

public record GetCurrentHandResponse(
	Guid HandId,
	string Phase,
	int Pot,
	Guid? CurrentPlayerToAct,
	int CurrentBet,
	List<HandPlayerStateResponse> Players
);

public record HandPlayerStateResponse(
	Guid PlayerId,
	string Name,
	int ChipStack,
	int CurrentBet,
	string Status,
	int CardCount
);

public record GetPlayerCardsResponse(
	Guid PlayerId,
	List<string> Cards,
	int CardCount
);

public record DealCardsResponse(
	bool Success,
	string Phase,
	List<PlayerCardCountResponse> PlayerCardCounts,
	Guid? CurrentPlayerToAct
);

public record PlayerCardCountResponse(
	Guid PlayerId,
	string PlayerName,
	int CardCount
);

public record GetAvailableActionsResponse(
	Guid PlayerId,
	bool IsCurrentPlayer,
	AvailableActionsDto Actions
);

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

public record PlaceActionRequest(
	Guid PlayerId,
	BettingActionType ActionType,
	int Amount = 0
);

public record PlaceActionResponse(
	bool Success,
	string ActionDescription,
	int NewPot,
	Guid? NextPlayerToAct,
	bool RoundComplete,
	bool PhaseAdvanced,
	string CurrentPhase,
	string? ErrorMessage = null
);
// Request DTOs
public record CreateGameRequest(
	GameType GameType,
	CreateGameConfigurationRequest? Configuration = null
);

public record CreateGameConfigurationRequest(
	int? Ante = null,
	int? MinBet = null,
	int? StartingChips = null,
	int? MaxPlayers = null
);

public record JoinGameRequest(
	string PlayerName,
	int? BuyIn = null
);

// Response DTOs
public record CreateGameResponse(
	Guid GameId,
	GameType GameType,
	string Status,
	GameConfigurationResponse Configuration,
	DateTime CreatedAt
);

public record GameConfigurationResponse(
	int Ante,
	int MinBet,
	int StartingChips,
	int MaxPlayers
);

public record JoinGameResponse(
	Guid PlayerId,
	string Name,
	int ChipStack,
	int Position,
	string Status
);

public record GetGameStateResponse(
	Guid GameId,
	GameType GameType,
	string Status,
	GameConfigurationResponse Configuration,
	List<PlayerStateResponse> Players,
	int DealerPosition,
	DateTime CreatedAt
);

public record PlayerStateResponse(
	Guid PlayerId,
	string Name,
	int ChipStack,
	int Position,
	string Status
);