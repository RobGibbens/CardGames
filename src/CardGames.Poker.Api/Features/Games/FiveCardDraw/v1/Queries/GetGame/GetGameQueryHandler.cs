using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Games;
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
		var gameTypeCode = await context.Games
			.Where(g => g.Id == request.GameId)
			.Select(g => g.GameType.Code)
			.AsNoTracking()
			.FirstOrDefaultAsync(cancellationToken);

		if (!PokerGameMetadataRegistry.TryGet(gameTypeCode, out var metadata) || metadata is null)
		{
			return null;
		}
		
		return await hybridCache.GetOrCreateAsync(
			$"{Feature.Version}-{request.CacheKey}",
			async _ =>
				await context.Games
					.Where(g => g.Id == request.GameId)
					.AsNoTracking()
					.ProjectToResponse(metadata.MinimumNumberOfPlayers, metadata.MaximumNumberOfPlayers)
					.FirstOrDefaultAsync(cancellationToken),
			cancellationToken: cancellationToken,
			tags: [Feature.Version, Feature.Name, nameof(GetGameQuery)]
		);
	}
}
