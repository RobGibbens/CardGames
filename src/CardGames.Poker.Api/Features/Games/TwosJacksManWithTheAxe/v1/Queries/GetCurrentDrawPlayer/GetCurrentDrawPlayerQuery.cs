using MediatR;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetCurrentDrawPlayer;

/// <summary>
/// Query to retrieve the current player who must act during the draw phase for a specific game.
/// </summary>
public record GetCurrentDrawPlayerQuery(Guid GameId) : IRequest<GetCurrentDrawPlayerResponse?>
{
	public string CacheKey => $"{Feature.Name}:{Feature.Version}:{nameof(GetCurrentDrawPlayerQuery)}:{GameId}";
}
