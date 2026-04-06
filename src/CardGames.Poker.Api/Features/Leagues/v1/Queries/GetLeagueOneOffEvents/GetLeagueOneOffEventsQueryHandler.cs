using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueOneOffEvents;

public sealed class GetLeagueOneOffEventsQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetLeagueOneOffEventsQuery, OneOf<IReadOnlyList<LeagueOneOffEventDto>, GetLeagueOneOffEventsError>>
{
	public async Task<OneOf<IReadOnlyList<LeagueOneOffEventDto>, GetLeagueOneOffEventsError>> Handle(GetLeagueOneOffEventsQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetLeagueOneOffEventsError(GetLeagueOneOffEventsErrorCode.Unauthorized, "User is not authenticated.");
		}

		var isMember = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == request.LeagueId &&
				x.UserId == currentUserService.UserId &&
				x.IsActive,
				cancellationToken);

		if (!isMember)
		{
			return new GetLeagueOneOffEventsError(GetLeagueOneOffEventsErrorCode.Forbidden, "Only active members can view one-off events.");
		}

		var events = await context.LeagueOneOffEvents
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId)
			.OrderBy(x => x.ScheduledAtUtc)
			.ThenBy(x => x.CreatedAtUtc)
			.Select(x => new LeagueOneOffEventDto
			{
				EventId = x.Id,
				LeagueId = x.LeagueId,
				Name = x.Name,
				ScheduledAtUtc = x.ScheduledAtUtc,
				EventType = (Contracts.LeagueOneOffEventType)x.EventType,
				Status = (Contracts.LeagueOneOffEventStatus)x.Status,
				Notes = x.Notes,
				CreatedByUserId = x.CreatedByUserId,
				CreatedAtUtc = x.CreatedAtUtc,
				LaunchedGameId = x.LaunchedGameId,
				GameTypeCode = x.GameTypeCode,
				Ante = x.Ante,
				MinBet = x.MinBet
			})
			.ToListAsync(cancellationToken);

		return events;
	}
}