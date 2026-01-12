using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGames;

public record GetGamesQuery() : IRequest<List<GetGamesResponse>>
{
	public string CacheKey => $"{Feature.Name}:{Feature.Version}:{nameof(GetGamesQuery)}";
}