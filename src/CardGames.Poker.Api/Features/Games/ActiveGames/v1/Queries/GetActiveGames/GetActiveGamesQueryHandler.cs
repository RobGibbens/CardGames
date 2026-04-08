using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;

public class GetActiveGamesQueryHandler(CardsDbContext context, HybridCache hybridCache, ICurrentUserService currentUserService)
	: IRequestHandler<GetActiveGamesQuery, List<GetActiveGamesResponse>>
{
	private const string CompletePhase = "Complete";

	public async Task<List<GetActiveGamesResponse>> Handle(GetActiveGamesQuery request, CancellationToken cancellationToken)
	{
		var activeGames = await hybridCache.GetOrCreateAsync(
			$"{Feature.Version}-{request.CacheKey}",
			async _ =>
			{
				var query = context.Games
					.Include(g => g.GameType)
					.Where(g => !g.IsDeleted)
					.Where(g => g.CurrentPhase != CompletePhase);

				if (!string.IsNullOrWhiteSpace(request.Variant))
				{
					query = query.Where(g => g.GameType.Name == request.Variant || g.GameType.Code == request.Variant);
				}

				var results = await query
					.OrderBy(g => g.Name)
					.AsNoTracking()
					.ProjectToResponse()
					.ToListAsync(cancellationToken);

				var gameIds = results.Select(x => x.Id).ToArray();
				var leagueGameIds = new Dictionary<Guid, Guid>();

				var seasonEventLinks = await context.LeagueSeasonEvents
					.AsNoTracking()
					.Where(x => x.LaunchedGameId.HasValue)
					.Where(x => x.Status != Data.Entities.LeagueSeasonEventStatus.Completed)
					.Where(x => x.Status != Data.Entities.LeagueSeasonEventStatus.Canceled)
					.Where(x => gameIds.Contains(x.LaunchedGameId!.Value))
					.Select(x => new { GameId = x.LaunchedGameId!.Value, x.LeagueId })
					.ToListAsync(cancellationToken);

				foreach (var link in seasonEventLinks)
				{
					leagueGameIds[link.GameId] = link.LeagueId;
				}

				var oneOffEventLinks = await context.LeagueOneOffEvents
					.AsNoTracking()
					.Where(x => x.LaunchedGameId.HasValue)
					.Where(x => x.Status != Data.Entities.LeagueOneOffEventStatus.Completed)
					.Where(x => x.Status != Data.Entities.LeagueOneOffEventStatus.Canceled)
					.Where(x => gameIds.Contains(x.LaunchedGameId!.Value))
					.Select(x => new { GameId = x.LaunchedGameId!.Value, x.LeagueId })
					.ToListAsync(cancellationToken);

				foreach (var link in oneOffEventLinks)
				{
					leagueGameIds[link.GameId] = link.LeagueId;
				}

				return results
					.Select(r =>
					{
						var phaseDescription = PhaseDescriptionResolver.TryResolve(r.GameTypeCode, r.CurrentPhase);
						var leagueId = leagueGameIds.TryGetValue(r.Id, out var linkedLeagueId)
							? linkedLeagueId
							: (Guid?)null;

						if (!PokerGameMetadataRegistry.TryGet(r.GameTypeCode, out var metadata) || metadata is null)
						{
							return r with
							{
								CurrentPhaseDescription = phaseDescription,
								LeagueId = leagueId
							};
						}

						return r with
						{
							GameTypeMetadataName = metadata.Name,
							GameTypeDescription = metadata.Description,
							GameTypeImageName = metadata.ImageName,
							CurrentPhaseDescription = phaseDescription,
							LeagueId = leagueId
						};
					})
					.ToList();
			},
			cancellationToken: cancellationToken,
			tags: [Feature.Version, Feature.Name, nameof(GetActiveGamesQuery)]
		);

		if (!activeGames.Any(x => x.LeagueId.HasValue))
		{
			return activeGames;
		}

		var accessibleLeagueIds = await GetAccessibleLeagueIdsAsync(cancellationToken);

		return activeGames
			.Where(x => !x.LeagueId.HasValue || accessibleLeagueIds.Contains(x.LeagueId.Value))
			.ToList();
	}

	private async Task<HashSet<Guid>> GetAccessibleLeagueIdsAsync(CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return [];
		}

		var userId = currentUserService.UserId;
		var membershipLeagueIds = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.UserId == userId && x.IsActive)
			.Select(x => x.LeagueId)
			.ToListAsync(cancellationToken);

		var accessibleLeagueIds = membershipLeagueIds.ToHashSet();

		var ownedLeagueIds = await context.Leagues
			.AsNoTracking()
			.Where(x => x.CreatedByUserId == userId)
			.Select(x => x.Id)
			.ToListAsync(cancellationToken);

		accessibleLeagueIds.UnionWith(ownedLeagueIds);
		return accessibleLeagueIds;
	}
}
