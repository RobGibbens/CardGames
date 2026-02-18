using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueSeasonEvents;

public sealed class GetLeagueSeasonEventsQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetLeagueSeasonEventsQuery, OneOf<IReadOnlyList<LeagueSeasonEventDto>, GetLeagueSeasonEventsError>>
{
	public async Task<OneOf<IReadOnlyList<LeagueSeasonEventDto>, GetLeagueSeasonEventsError>> Handle(GetLeagueSeasonEventsQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetLeagueSeasonEventsError(GetLeagueSeasonEventsErrorCode.Unauthorized, "User is not authenticated.");
		}

		var isMember = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == request.LeagueId &&
				x.UserId == currentUserService.UserId &&
				x.IsActive,
				cancellationToken);

		if (!isMember)
		{
			return new GetLeagueSeasonEventsError(GetLeagueSeasonEventsErrorCode.Forbidden, "Only active members can view season events.");
		}

		var seasonExists = await context.LeagueSeasons
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.SeasonId && x.LeagueId == request.LeagueId, cancellationToken);

		if (!seasonExists)
		{
			return new GetLeagueSeasonEventsError(GetLeagueSeasonEventsErrorCode.SeasonNotFound, "Season not found in league.");
		}

		var events = await context.LeagueSeasonEvents
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.LeagueSeasonId == request.SeasonId)
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
				CreatedAtUtc = x.CreatedAtUtc
			})
			.ToListAsync(cancellationToken);

		return events;
	}
}