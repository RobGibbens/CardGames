using MediatR;
using SharedGetCurrentPlayerTurnResponse = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Queries.GetCurrentPlayerTurn.GetCurrentPlayerTurnResponse;

namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Queries.GetCurrentPlayerTurn;

public record GetCurrentPlayerTurnQuery(Guid GameId) : IRequest<SharedGetCurrentPlayerTurnResponse?>
{
	public string CacheKey => $"Tollbooth:v1:{nameof(GetCurrentPlayerTurnQuery)}:{GameId}";
}
