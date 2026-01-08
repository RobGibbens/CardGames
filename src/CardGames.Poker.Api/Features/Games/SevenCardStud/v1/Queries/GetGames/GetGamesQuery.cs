using MediatR;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Queries.GetGames;

public record GetGamesQuery() : IRequest<List<GetGamesResponse>>
{
	public string CacheKey => $"{Feature.Name}:{Feature.Version}:{nameof(GetGamesQuery)}";
}