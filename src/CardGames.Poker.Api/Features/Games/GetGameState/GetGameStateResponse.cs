using CardGames.Poker.Api.Features.Games.Domain;
using CardGames.Poker.Api.Features.Games.Domain.Enums;

namespace CardGames.Poker.Api.Features.Games.GetGameState;

/// <summary>
/// Response containing the current state of a poker game.
/// </summary>
public record GetGameStateResponse(
	Guid GameId,
	GameType GameType,
	GameStatus Status,
	GameConfiguration Configuration,
	List<PlayerStateResponse> Players,
	int DealerPosition,
	DateTime CreatedAt
);