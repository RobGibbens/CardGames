using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueOneOffEventsPage;

public sealed class GetLeagueOneOffEventsPageQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetLeagueOneOffEventsPageQuery, OneOf<LeagueOneOffEventsPageDto, GetLeagueOneOffEventsPageError>>
{
	public async Task<OneOf<LeagueOneOffEventsPageDto, GetLeagueOneOffEventsPageError>> Handle(GetLeagueOneOffEventsPageQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetLeagueOneOffEventsPageError(GetLeagueOneOffEventsPageErrorCode.Unauthorized, "User is not authenticated.");
		}

		var isMember = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == request.LeagueId &&
				x.UserId == currentUserService.UserId &&
				x.IsActive,
				cancellationToken);

		if (!isMember)
		{
			return new GetLeagueOneOffEventsPageError(GetLeagueOneOffEventsPageErrorCode.Forbidden, "Only active members can view one-off events.");
		}

		var pageSize = Math.Clamp(request.PageSize, 1, 100);
		var pageNumber = Math.Max(request.PageNumber, 1);

		var query =
			from oneOffEvent in context.LeagueOneOffEvents.AsNoTracking()
			where oneOffEvent.LeagueId == request.LeagueId
				&& (request.IncludeCompleted || oneOffEvent.Status != Data.Entities.LeagueOneOffEventStatus.Completed)
			join gameType in context.GameTypes.AsNoTracking()
				on oneOffEvent.GameTypeCode equals gameType.Code into gameTypes
			from gameType in gameTypes.DefaultIfEmpty()
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
			};

		var totalCount = await query.CountAsync(cancellationToken);
		var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

		if (pageNumber > totalPages)
		{
			pageNumber = totalPages;
		}

		var skip = (pageNumber - 1) * pageSize;
		var entries = await query
			.OrderBy(x => x.ScheduledAtUtc)
			.ThenBy(x => x.CreatedAtUtc)
			.Skip(skip)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		return new LeagueOneOffEventsPageDto
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