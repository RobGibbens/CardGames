using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetRecentCompletedLeagueSeasonEvents;

public sealed class GetRecentCompletedLeagueSeasonEventsQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetRecentCompletedLeagueSeasonEventsQuery, OneOf<IReadOnlyList<LeagueSeasonEventDto>, GetRecentCompletedLeagueSeasonEventsError>>
{
	public async Task<OneOf<IReadOnlyList<LeagueSeasonEventDto>, GetRecentCompletedLeagueSeasonEventsError>> Handle(GetRecentCompletedLeagueSeasonEventsQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetRecentCompletedLeagueSeasonEventsError(GetRecentCompletedLeagueSeasonEventsErrorCode.Unauthorized, "User is not authenticated.");
		}

		var isMember = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive, cancellationToken);

		if (!isMember)
		{
			return new GetRecentCompletedLeagueSeasonEventsError(GetRecentCompletedLeagueSeasonEventsErrorCode.Forbidden, "Only active members can view season events.");
		}

		var seasonExists = await context.LeagueSeasons
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.SeasonId && x.LeagueId == request.LeagueId, cancellationToken);

		if (!seasonExists)
		{
			return new GetRecentCompletedLeagueSeasonEventsError(GetRecentCompletedLeagueSeasonEventsErrorCode.SeasonNotFound, "Season not found in league.");
		}

		var take = Math.Clamp(request.Take, 1, 20);
		var events = await context.LeagueSeasonEvents
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.LeagueSeasonId == request.SeasonId && x.Status == Data.Entities.LeagueSeasonEventStatus.Completed)
			.OrderByDescending(x => x.ScheduledAtUtc ?? x.CreatedAtUtc)
			.ThenByDescending(x => x.CreatedAtUtc)
			.Take(take)
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
			.ToListAsync(cancellationToken);

		return events;
	}
}