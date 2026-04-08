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

		var events = await (
			from oneOffEvent in context.LeagueOneOffEvents.AsNoTracking()
			where oneOffEvent.LeagueId == request.LeagueId
			join gameType in context.GameTypes.AsNoTracking()
				on oneOffEvent.GameTypeCode equals gameType.Code into gameTypes
			from gameType in gameTypes.DefaultIfEmpty()
			orderby oneOffEvent.ScheduledAtUtc, oneOffEvent.CreatedAtUtc
			select new LeagueOneOffEventDto
			{
				EventId = oneOffEvent.Id,
				LeagueId = oneOffEvent.LeagueId,
				Name = oneOffEvent.Name,
				ScheduledAtUtc = oneOffEvent.ScheduledAtUtc,
				EventType = (Contracts.LeagueOneOffEventType)oneOffEvent.EventType,
				Status = (Contracts.LeagueOneOffEventStatus)oneOffEvent.Status,
				Notes = oneOffEvent.Notes,
				CreatedByUserId = oneOffEvent.CreatedByUserId,
				CreatedAtUtc = oneOffEvent.CreatedAtUtc,
				LaunchedGameId = oneOffEvent.LaunchedGameId,
				GameTypeCode = oneOffEvent.GameTypeCode,
				GameTypeName = gameType != null ? gameType.Name : null,
				Ante = oneOffEvent.Ante,
				MinBet = oneOffEvent.MinBet,
				SmallBlind = oneOffEvent.SmallBlind,
				BigBlind = oneOffEvent.BigBlind,
				TournamentBuyIn = oneOffEvent.TournamentBuyIn
			})
			.ToListAsync(cancellationToken);

		return events;
	}
}