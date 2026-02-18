using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueSeasons;

public sealed class GetLeagueSeasonsQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetLeagueSeasonsQuery, OneOf<IReadOnlyList<LeagueSeasonDto>, GetLeagueSeasonsError>>
{
	public async Task<OneOf<IReadOnlyList<LeagueSeasonDto>, GetLeagueSeasonsError>> Handle(GetLeagueSeasonsQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetLeagueSeasonsError(GetLeagueSeasonsErrorCode.Unauthorized, "User is not authenticated.");
		}

		var isMember = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == request.LeagueId &&
				x.UserId == currentUserService.UserId &&
				x.IsActive,
				cancellationToken);

		if (!isMember)
		{
			return new GetLeagueSeasonsError(GetLeagueSeasonsErrorCode.Forbidden, "Only active members can view seasons.");
		}

		var seasons = await context.LeagueSeasons
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId)
			.OrderByDescending(x => x.CreatedAtUtc)
			.Select(x => new LeagueSeasonDto
			{
				SeasonId = x.Id,
				LeagueId = x.LeagueId,
				Name = x.Name,
				PlannedEventCount = x.PlannedEventCount,
				StartsAtUtc = x.StartsAtUtc,
				EndsAtUtc = x.EndsAtUtc,
				Status = (Contracts.LeagueSeasonStatus)x.Status,
				CreatedByUserId = x.CreatedByUserId,
				CreatedAtUtc = x.CreatedAtUtc
			})
			.ToListAsync(cancellationToken);

		return seasons;
	}
}