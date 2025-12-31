using MediatR;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGame;

/// <summary>
/// Query to retrieve a specific game by its identifier.
/// Works for any game type.
/// </summary>
public record GetGameQuery(Guid GameId) : IRequest<GetGameResponse>
{
	public string CacheKey => $"games-common-{GameId}";
}
