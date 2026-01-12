namespace CardGames.Poker.Api.Features.Games.AvailablePokerGames.v1.Queries.GetAvailablePokerGames;

/// <summary>
/// Response containing details about an available poker game type.
/// </summary>
public record GetAvailablePokerGamesResponse(
	string Code,
	string Name,
	string Description,
	int MinimumNumberOfPlayers,
	int MaximumNumberOfPlayers,
	string? ImageName = null
);
