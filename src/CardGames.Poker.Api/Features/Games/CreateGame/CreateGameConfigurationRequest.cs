namespace CardGames.Poker.Api.Features.Games.CreateGame;

/// <summary>
/// Optional configuration overrides for game creation.
/// </summary>
public record CreateGameConfigurationRequest(
	int? Ante = null,
	int? MinBet = null,
	int? StartingChips = null,
	int? MaxPlayers = null
);