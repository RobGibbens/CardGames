using MediatR;

namespace CardGames.Poker.Api.Features.Games.AvailablePokerGames.v1.Queries.GetAvailablePokerGames;

public record GetAvailablePokerGamesQuery(string? Variant = null) : IRequest<List<GetAvailablePokerGamesResponse>>
{
	public string CacheKey => $"{Feature.Name}:{Feature.Version}:{nameof(GetAvailablePokerGamesQuery)}:{Variant}";
}
