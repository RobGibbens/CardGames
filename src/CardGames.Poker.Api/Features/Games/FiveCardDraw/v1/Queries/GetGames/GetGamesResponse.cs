using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGames;

public record GetGamesResponse(
	Guid Id,
	string? Name,
	string CurrentPhase,
	GameStatus Status,
	DateTimeOffset CreatedAt,
	string RowVersion
);