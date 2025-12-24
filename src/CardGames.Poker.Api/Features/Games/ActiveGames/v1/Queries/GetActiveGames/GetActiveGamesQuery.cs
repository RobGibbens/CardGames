using MediatR;

namespace CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;

public record GetActiveGamesQuery : IRequest<List<GetActiveGamesResponse>>
{
	public string CacheKey => "GetActiveGames";
}
