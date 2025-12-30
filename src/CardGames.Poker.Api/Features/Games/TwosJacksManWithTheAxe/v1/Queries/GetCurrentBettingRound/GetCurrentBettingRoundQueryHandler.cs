using CardGames.Poker.Api.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetCurrentBettingRound;

/// <summary>
/// Handler for retrieving the current betting round for a specific game.
/// </summary>
public class GetCurrentBettingRoundQueryHandler(CardsDbContext context, HybridCache hybridCache)
	: IRequestHandler<GetCurrentBettingRoundQuery, GetCurrentBettingRoundResponse?>
{
	public async Task<GetCurrentBettingRoundResponse?> Handle(GetCurrentBettingRoundQuery request, CancellationToken cancellationToken)
	{
		return await hybridCache.GetOrCreateAsync(
			$"{Feature.Version}-{request.CacheKey}",
			async _ =>
						await context.BettingRounds
							.Include(br => br.Game)
								.ThenInclude(g => g.Pots)
							.Where(br => br.GameId == request.GameId && !br.IsComplete)
							.OrderByDescending(br => br.HandNumber)
					.ThenByDescending(br => br.RoundNumber)
					.AsNoTracking()
					.ProjectToResponse()
					.FirstOrDefaultAsync(cancellationToken),
			cancellationToken: cancellationToken,
			tags: [Feature.Version, Feature.Name, nameof(GetCurrentBettingRoundQuery)]
		);
	}
}
