using CardGames.Poker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGame;

/// <summary>
/// Handler for retrieving a specific game by its identifier.
/// </summary>
public class GetGameQueryHandler(CardsDbContext context, HybridCache hybridCache)
	: IRequestHandler<GetGameQuery, GetGameResponse?>
{
	public async Task<GetGameResponse?> Handle(GetGameQuery request, CancellationToken cancellationToken)
	{
		return await hybridCache.GetOrCreateAsync(
			$"{Feature.Version}-{request.CacheKey}",
			async _ =>
				await context.Games
					.Where(g => g.Id == request.GameId)
					.AsNoTracking()
					.ProjectToResponse()
					.FirstOrDefaultAsync(cancellationToken),
			cancellationToken: cancellationToken,
			tags: [Feature.Version, Feature.Name, nameof(GetGameQuery)]
		);
	}
}
