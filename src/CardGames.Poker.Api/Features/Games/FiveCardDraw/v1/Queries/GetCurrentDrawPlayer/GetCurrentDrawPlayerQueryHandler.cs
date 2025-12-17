using CardGames.Poker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetCurrentDrawPlayer;

/// <summary>
/// Handler for retrieving the current player who must act during the draw phase.
/// </summary>
public class GetCurrentDrawPlayerQueryHandler(CardsDbContext context, HybridCache hybridCache)
	: IRequestHandler<GetCurrentDrawPlayerQuery, GetCurrentDrawPlayerResponse?>
{
	public async Task<GetCurrentDrawPlayerResponse?> Handle(GetCurrentDrawPlayerQuery request, CancellationToken cancellationToken)
	{
		return await hybridCache.GetOrCreateAsync(
			$"{Feature.Version}-{request.CacheKey}",
			async _ =>
			{
				var game = await context.Games
					.AsNoTracking()
					.FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

				// Return null if game not found or not in draw phase
				if (game is null || game.CurrentPhase != "DrawPhase" || game.CurrentDrawPlayerIndex < 0)
				{
					return null;
				}

				var gamePlayer = await context.GamePlayers
					.Where(gp => gp.GameId == request.GameId && gp.SeatPosition == game.CurrentDrawPlayerIndex)
					.Include(gp => gp.Player)
					.Include(gp => gp.Cards)
					.AsNoTracking()
					.FirstOrDefaultAsync(cancellationToken);

				return gamePlayer?.ToResponse();
			},
			cancellationToken: cancellationToken,
			tags: [Feature.Version, Feature.Name, nameof(GetCurrentDrawPlayerQuery)]
		);
	}
}
