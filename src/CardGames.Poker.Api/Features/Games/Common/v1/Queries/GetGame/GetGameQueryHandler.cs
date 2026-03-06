using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Games;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGame;

/// <summary>
/// Handler for retrieving a specific game by its identifier.
/// Works for any game type - returns the game with its type code.
/// </summary>
public class GetGameQueryHandler(CardsDbContext context, HybridCache hybridCache)
	: IRequestHandler<GetGameQuery, GetGameResponse?>
{
	public async Task<GetGameResponse?> Handle(GetGameQuery request, CancellationToken cancellationToken)
	{
		var gameInfo = await context.Games
			.Where(g => g.Id == request.GameId && !g.IsDeleted)
			.Select(g => new { g.GameType.Code, g.IsDealersChoice })
			.AsNoTracking()
			.FirstOrDefaultAsync(cancellationToken);

		int minPlayers = 2, maxPlayers = 8;
		if (gameInfo is null)
		{
			return null;
		}

		if (!gameInfo.IsDealersChoice)
		{
			if (!PokerGameMetadataRegistry.TryGet(gameInfo.Code, out var metadata) || metadata is null)
			{
				return null;
			}

			minPlayers = metadata.MinimumNumberOfPlayers;
			maxPlayers = metadata.MaximumNumberOfPlayers;
		}
		
		return await hybridCache.GetOrCreateAsync(
			$"{Feature.Version}-{request.CacheKey}",
			async _ =>
			{
				var response = await context.Games
					.Include(g => g.GameType)
					.Where(g => g.Id == request.GameId)
					.AsNoTracking()
					.ProjectToResponse(minPlayers, maxPlayers)
					.FirstOrDefaultAsync(cancellationToken);

				if (response is null)
				{
					return null;
				}
				
				return response;
			},
			cancellationToken: cancellationToken,
			tags: [Feature.Version, Feature.Name, nameof(GetGameQuery)]
		);
	}
}
