using MediatR;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGame;

/// <summary>
/// Query to retrieve a specific game by its identifier.
/// </summary>
public record GetGameQuery(Guid GameId) : IRequest<GetGameResponse?>
{
	public string CacheKey => $"{Feature.Name}:{Feature.Version}:{nameof(GetGameQuery)}:{GameId}";
}
