using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueUpcomingEventsPage;

public sealed class GetLeagueUpcomingEventsPageQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetLeagueUpcomingEventsPageQuery, OneOf<LeagueUpcomingEventsPageDto, GetLeagueUpcomingEventsPageError>>
{
	private sealed record UpcomingEventRow
	{
		public required DateTimeOffset SortAt { get; init; }

		public required DateTimeOffset CreatedAtUtc { get; init; }

		public required string Name { get; init; }

		public required bool IsSeasonEvent { get; init; }

		public Guid EventId { get; init; }

		public Guid LeagueId { get; init; }

		public Guid? SeasonId { get; init; }

		public int? SequenceNumber { get; init; }

		public DateTimeOffset? ScheduledAtUtc { get; init; }

		public int StatusValue { get; init; }

		public string? Notes { get; init; }

		public required string CreatedByUserId { get; init; }

		public Guid? LaunchedGameId { get; init; }

		public int? TournamentBuyIn { get; init; }

		public int? OneOffEventTypeValue { get; init; }

		public string? GameTypeCode { get; init; }

		public string? GameTypeName { get; init; }

		public int? Ante { get; init; }

		public int? MinBet { get; init; }

		public int? SmallBlind { get; init; }

		public int? BigBlind { get; init; }
	}

	public async Task<OneOf<LeagueUpcomingEventsPageDto, GetLeagueUpcomingEventsPageError>> Handle(GetLeagueUpcomingEventsPageQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetLeagueUpcomingEventsPageError(GetLeagueUpcomingEventsPageErrorCode.Unauthorized, "User is not authenticated.");
		}

		var isMember = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive, cancellationToken);

		if (!isMember)
		{
			return new GetLeagueUpcomingEventsPageError(GetLeagueUpcomingEventsPageErrorCode.Forbidden, "Only active members can view upcoming league events.");
		}

		var pageSize = Math.Clamp(request.PageSize, 1, 100);
		var pageNumber = Math.Max(request.PageNumber, 1);

		var seasonEvents = context.LeagueSeasonEvents
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId
				&& !x.LaunchedGameId.HasValue
				&& x.Status != Data.Entities.LeagueSeasonEventStatus.Completed
				&& x.Status != Data.Entities.LeagueSeasonEventStatus.Canceled)
			.Select(x => new UpcomingEventRow
			{
				SortAt = x.ScheduledAtUtc ?? x.CreatedAtUtc,
				CreatedAtUtc = x.CreatedAtUtc,
				Name = x.Name,
				IsSeasonEvent = true,
				EventId = x.Id,
				LeagueId = x.LeagueId,
				SeasonId = x.LeagueSeasonId,
				SequenceNumber = x.SequenceNumber,
				ScheduledAtUtc = x.ScheduledAtUtc,
				StatusValue = (int)x.Status,
				Notes = x.Notes,
				CreatedByUserId = x.CreatedByUserId,
				LaunchedGameId = x.LaunchedGameId,
				TournamentBuyIn = x.TournamentBuyIn,
				OneOffEventTypeValue = null,
				GameTypeCode = x.GameTypeCode,
				GameTypeName = null,
				Ante = x.Ante,
				MinBet = x.MinBet,
				SmallBlind = x.SmallBlind,
				BigBlind = x.BigBlind
			});

		var oneOffEvents =
			from oneOffEvent in context.LeagueOneOffEvents.AsNoTracking()
			where oneOffEvent.LeagueId == request.LeagueId
				&& !oneOffEvent.LaunchedGameId.HasValue
				&& oneOffEvent.Status != Data.Entities.LeagueOneOffEventStatus.Completed
				&& oneOffEvent.Status != Data.Entities.LeagueOneOffEventStatus.Canceled
			join gameType in context.GameTypes.AsNoTracking() on oneOffEvent.GameTypeCode equals gameType.Code into gameTypes
			from gameType in gameTypes.DefaultIfEmpty()
			select new UpcomingEventRow
			{
				SortAt = oneOffEvent.ScheduledAtUtc,
				CreatedAtUtc = oneOffEvent.CreatedAtUtc,
				Name = oneOffEvent.Name,
				IsSeasonEvent = false,
				EventId = oneOffEvent.Id,
				LeagueId = oneOffEvent.LeagueId,
				SeasonId = null,
				SequenceNumber = null,
				ScheduledAtUtc = oneOffEvent.ScheduledAtUtc,
				StatusValue = (int)oneOffEvent.Status,
				Notes = oneOffEvent.Notes,
				CreatedByUserId = oneOffEvent.CreatedByUserId,
				LaunchedGameId = oneOffEvent.LaunchedGameId,
				TournamentBuyIn = oneOffEvent.TournamentBuyIn,
				OneOffEventTypeValue = (int)oneOffEvent.EventType,
				GameTypeCode = oneOffEvent.GameTypeCode,
				GameTypeName = gameType != null ? gameType.Name : null,
				Ante = oneOffEvent.Ante,
				MinBet = oneOffEvent.MinBet,
				SmallBlind = oneOffEvent.SmallBlind,
				BigBlind = oneOffEvent.BigBlind
			};

		var query = seasonEvents.Concat(oneOffEvents);
		var totalCount = await query.CountAsync(cancellationToken);
		var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

		if (pageNumber > totalPages)
		{
			pageNumber = totalPages;
		}

		var skip = (pageNumber - 1) * pageSize;
		var rows = await query
			.OrderBy(x => x.SortAt)
			.ThenBy(x => x.CreatedAtUtc)
			.ThenBy(x => x.Name)
			.Skip(skip)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		var entries = rows.Select(row => row.IsSeasonEvent
			? new LeagueUpcomingEventEntryDto
			{
				SortAt = row.SortAt,
				SeasonEvent = new LeagueSeasonEventDto
				{
					EventId = row.EventId,
					LeagueId = row.LeagueId,
					SeasonId = row.SeasonId ?? Guid.Empty,
					Name = row.Name,
					SequenceNumber = row.SequenceNumber,
					ScheduledAtUtc = row.ScheduledAtUtc,
					Status = (Contracts.LeagueSeasonEventStatus)row.StatusValue,
					Notes = row.Notes,
					CreatedByUserId = row.CreatedByUserId,
					CreatedAtUtc = row.CreatedAtUtc,
					LaunchedGameId = row.LaunchedGameId,
					GameTypeCode = row.GameTypeCode,
					Ante = row.Ante,
					MinBet = row.MinBet,
					SmallBlind = row.SmallBlind,
					BigBlind = row.BigBlind,
					TournamentBuyIn = row.TournamentBuyIn
				},
				OneOffEvent = null
			}
			: new LeagueUpcomingEventEntryDto
			{
				SortAt = row.SortAt,
				SeasonEvent = null,
				OneOffEvent = new LeagueOneOffEventDto
				{
					EventId = row.EventId,
					LeagueId = row.LeagueId,
					Name = row.Name,
					ScheduledAtUtc = row.ScheduledAtUtc ?? row.SortAt,
					EventType = (Contracts.LeagueOneOffEventType)(row.OneOffEventTypeValue ?? 0),
					Status = (Contracts.LeagueOneOffEventStatus)row.StatusValue,
					Notes = row.Notes,
					CreatedByUserId = row.CreatedByUserId,
					CreatedAtUtc = row.CreatedAtUtc,
					LaunchedGameId = row.LaunchedGameId,
					GameTypeCode = row.GameTypeCode,
					GameTypeName = row.GameTypeName,
					Ante = row.Ante ?? 0,
					MinBet = row.MinBet ?? 0,
					SmallBlind = row.SmallBlind,
					BigBlind = row.BigBlind,
					TournamentBuyIn = row.TournamentBuyIn
				}
			}).ToList();

		return new LeagueUpcomingEventsPageDto
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