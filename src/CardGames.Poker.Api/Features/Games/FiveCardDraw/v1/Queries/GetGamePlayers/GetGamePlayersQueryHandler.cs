using CardGames.Poker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGamePlayers;

/// <summary>
/// Handler for retrieving all players in a specific game.
/// </summary>
public class GetGamePlayersQueryHandler(CardsDbContext context, HybridCache hybridCache)
	: IRequestHandler<GetGamePlayersQuery, List<GetGamePlayersResponse>>
{
	public async Task<List<GetGamePlayersResponse>> Handle(GetGamePlayersQuery request, CancellationToken cancellationToken)
	{
		return await hybridCache.GetOrCreateAsync(
			$"{Feature.Version}-{request.CacheKey}",
			async _ =>
				await context.GamePlayers
					.Where(gp => gp.GameId == request.GameId)
					.Include(gp => gp.Player)
					.Include(gp => gp.Cards)
					.OrderBy(gp => gp.SeatPosition)
					.AsNoTracking()
					.ProjectToResponse()
					.ToListAsync(cancellationToken),
			cancellationToken: cancellationToken,
			tags: [Feature.Version, Feature.Name, nameof(GetGamePlayersQuery)]
		);
	}
}
