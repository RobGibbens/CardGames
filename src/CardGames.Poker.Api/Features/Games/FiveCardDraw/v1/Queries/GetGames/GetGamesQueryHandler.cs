﻿using CardGames.Poker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGames;

public class GetGamesQueryHandler(CardsDbContext context, HybridCache hybridCache)
	: IRequestHandler<GetGamesQuery, List<GetGamesResponse>>
{
	public async Task<List<GetGamesResponse>> Handle(GetGamesQuery request, CancellationToken cancellationToken)
	{
		return await hybridCache.GetOrCreateAsync(
			$"{Feature.Version}-{request.CacheKey}",
			async _ =>
				await context.Games
					.Where(g => !g.IsDeleted)
					.OrderBy(c => c.Name)
					.AsNoTracking()
					.ProjectToResponse()
					.ToListAsync(cancellationToken),
			cancellationToken: cancellationToken,
			tags: [Feature.Version, Feature.Name, nameof(GetGamesQuery)]
		);
	}
}