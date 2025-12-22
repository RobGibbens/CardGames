using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;

public record GetActiveGamesResponse(
	Guid Id,
	Guid GameTypeId,
	string GameTypeCode,
	string GameTypeName,
	string? GameTypeMetadataName,
	string? GameTypeDescription,
	string? GameTypeImageName,
	string? Name,
	string CurrentPhase,
	GameStatus Status,
	DateTimeOffset CreatedAt,
	string RowVersion
);
