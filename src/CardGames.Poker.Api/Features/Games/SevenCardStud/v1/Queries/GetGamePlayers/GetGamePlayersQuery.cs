using MediatR;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Queries.GetGamePlayers;

/// <summary>
/// Query to retrieve all players in a specific game.
/// </summary>
public record GetGamePlayersQuery(Guid GameId) : IRequest<List<GetGamePlayersResponse>>
{
	public string CacheKey => $"{Feature.Name}:{Feature.Version}:{nameof(GetGamePlayersQuery)}:{GameId}";
}
