using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Enums;

namespace CardGames.Poker.Api.Features.Games.CreateGame;

/// <summary>
/// Response returned after successfully creating a game.
/// </summary>
public record CreateGameResponse(
	Guid GameId,
	GameType GameType,
	GameStatus Status,
	GameConfiguration Configuration,
	DateTime CreatedAt
);