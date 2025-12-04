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