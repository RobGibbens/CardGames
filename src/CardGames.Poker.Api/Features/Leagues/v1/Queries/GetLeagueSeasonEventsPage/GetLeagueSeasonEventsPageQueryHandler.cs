using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueSeasonEventsPage;

public sealed class GetLeagueSeasonEventsPageQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetLeagueSeasonEventsPageQuery, OneOf<LeagueSeasonEventsPageDto, GetLeagueSeasonEventsPageError>>
{
	public async Task<OneOf<LeagueSeasonEventsPageDto, GetLeagueSeasonEventsPageError>> Handle(GetLeagueSeasonEventsPageQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetLeagueSeasonEventsPageError(GetLeagueSeasonEventsPageErrorCode.Unauthorized, "User is not authenticated.");
		}

		var isMember = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == request.LeagueId &&
				x.UserId == currentUserService.UserId &&
				x.IsActive,
				cancellationToken);

		if (!isMember)
		{
			return new GetLeagueSeasonEventsPageError(GetLeagueSeasonEventsPageErrorCode.Forbidden, "Only active members can view season events.");
		}

		var seasonExists = await context.LeagueSeasons
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.SeasonId && x.LeagueId == request.LeagueId, cancellationToken);

		if (!seasonExists)
		{
			return new GetLeagueSeasonEventsPageError(GetLeagueSeasonEventsPageErrorCode.SeasonNotFound, "Season not found in league.");
		}

		var pageSize = Math.Clamp(request.PageSize, 1, 100);
		var pageNumber = Math.Max(request.PageNumber, 1);

		var query = context.LeagueSeasonEvents
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.LeagueSeasonId == request.SeasonId);

		var totalCount = await query.CountAsync(cancellationToken);
		var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

		if (pageNumber > totalPages)
		{
			pageNumber = totalPages;
		}

		var skip = (pageNumber - 1) * pageSize;
		var entries = await query
			.OrderBy(x => x.SequenceNumber.HasValue ? 0 : 1)
			.ThenBy(x => x.SequenceNumber)
			.ThenBy(x => x.ScheduledAtUtc)
			.ThenBy(x => x.CreatedAtUtc)
			.Select(x => new LeagueSeasonEventDto
			{
				EventId = x.Id,
				LeagueId = x.LeagueId,
				SeasonId = x.LeagueSeasonId,
				Name = x.Name,
				SequenceNumber = x.SequenceNumber,
				ScheduledAtUtc = x.ScheduledAtUtc,
				Status = (Contracts.LeagueSeasonEventStatus)x.Status,
				Notes = x.Notes,
				CreatedByUserId = x.CreatedByUserId,
				CreatedAtUtc = x.CreatedAtUtc,
				LaunchedGameId = x.LaunchedGameId,
				TournamentBuyIn = x.TournamentBuyIn
			})
			.Skip(skip)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		return new LeagueSeasonEventsPageDto
		{
			Entries = entries,
			HasMore = pageNumber < totalPages,
			TotalCount = totalCount,
			PageNumber = pageNumber,
			PageSize = pageSize,
			TotalPages = totalPages
		};
	}
}