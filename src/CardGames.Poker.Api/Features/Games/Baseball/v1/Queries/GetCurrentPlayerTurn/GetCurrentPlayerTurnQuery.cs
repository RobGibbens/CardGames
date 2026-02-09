using MediatR;

namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Queries.GetCurrentPlayerTurn;

public record GetCurrentPlayerTurnQuery(Guid GameId) : IRequest<GetCurrentPlayerTurnResponse?>
{
	public string CacheKey => $"{Feature.Name}:{Feature.Version}:{nameof(GetCurrentPlayerTurnQuery)}:{GameId}";
}
