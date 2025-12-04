using CardGames.Poker.Api.Features.Games.Domain.Enums;

namespace CardGames.Poker.Api.Features.Games.CreateGame;

/// <summary>
/// Request to create a new poker game.
/// </summary>
public record CreateGameRequest(
	GameType GameType,
	CreateGameConfigurationRequest? Configuration = null
);