using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Games;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;

public class GetActiveGamesQueryHandler(CardsDbContext context, HybridCache hybridCache)
	: IRequestHandler<GetActiveGamesQuery, List<GetActiveGamesResponse>>
{
	private const string CompletePhase = "Complete";

	public async Task<List<GetActiveGamesResponse>> Handle(GetActiveGamesQuery request, CancellationToken cancellationToken)
	{
		return await hybridCache.GetOrCreateAsync(
			$"{Feature.Version}-{request.CacheKey}",
			async _ =>
			{
				var results = await context.Games
					.Include(g => g.GameType)
					.Where(g => !g.IsDeleted)
					.Where(g => g.CurrentPhase != CompletePhase)
					.OrderBy(g => g.Name)
					.AsNoTracking()
					.ProjectToResponse()
					.ToListAsync(cancellationToken);

				return results
					.Select(r =>
					{
						var phaseDescription = PhaseDescriptionResolver.TryResolve(r.GameTypeCode, r.CurrentPhase);

						if (!PokerGameMetadataRegistry.TryGet(r.GameTypeCode, out var metadata) || metadata is null)
						{
							return r with { CurrentPhaseDescription = phaseDescription };
						}

						return r with
						{
							GameTypeMetadataName = metadata.Name,
							GameTypeDescription = metadata.Description,
							GameTypeImageName = metadata.ImageName,
							CurrentPhaseDescription = phaseDescription
						};
					})
					.ToList();
			},
			cancellationToken: cancellationToken,
			tags: [Feature.Version, Feature.Name, nameof(GetActiveGamesQuery)]
		);
	}
}
