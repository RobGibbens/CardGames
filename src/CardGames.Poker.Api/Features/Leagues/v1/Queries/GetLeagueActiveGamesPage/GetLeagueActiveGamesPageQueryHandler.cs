using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueActiveGamesPage;

public sealed class GetLeagueActiveGamesPageQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetLeagueActiveGamesPageQuery, OneOf<LeagueActiveGamesPageDto, GetLeagueActiveGamesPageError>>
{
	public async Task<OneOf<LeagueActiveGamesPageDto, GetLeagueActiveGamesPageError>> Handle(GetLeagueActiveGamesPageQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetLeagueActiveGamesPageError(GetLeagueActiveGamesPageErrorCode.Unauthorized, "User is not authenticated.");
		}

		var isMember = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive, cancellationToken);

		if (!isMember)
		{
			return new GetLeagueActiveGamesPageError(GetLeagueActiveGamesPageErrorCode.Forbidden, "Only active members can view league active games.");
		}

		var pageSize = Math.Clamp(request.PageSize, 1, 100);
		var pageNumber = Math.Max(request.PageNumber, 1);

		var seasonEvents =
			from seasonEvent in context.LeagueSeasonEvents.AsNoTracking()
			where seasonEvent.LeagueId == request.LeagueId
				&& seasonEvent.LaunchedGameId.HasValue
				&& seasonEvent.Status != Data.Entities.LeagueSeasonEventStatus.Completed
				&& seasonEvent.Status != Data.Entities.LeagueSeasonEventStatus.Canceled
			join game in context.Games.AsNoTracking() on (seasonEvent.LaunchedGameId ?? Guid.Empty) equals game.Id into games
			from game in games.DefaultIfEmpty()
			where game == null || game.Status != Data.Entities.GameStatus.Completed
			join gameType in context.GameTypes.AsNoTracking() on game.GameTypeId equals gameType.Id into gameTypes
			from gameType in gameTypes.DefaultIfEmpty()
			select new LeagueActiveGameEntryDto
			{
				GameId = seasonEvent.LaunchedGameId ?? Guid.Empty,
				Name = seasonEvent.Name,
				EventType = "Season Event",
				StartedAt = game != null ? game.StartedAt ?? game.CreatedAt : seasonEvent.ScheduledAtUtc ?? seasonEvent.CreatedAtUtc,
				CurrentPhase = game != null ? game.CurrentPhase : null,
				PlayerCount = game != null
					? context.GamePlayers.Count(player => player.GameId == game.Id && player.Status == Data.Entities.GamePlayerStatus.Active)
					: 0,
				CreatedByName = game != null ? game.CreatedByName ?? game.CreatedById ?? seasonEvent.CreatedByUserId : seasonEvent.CreatedByUserId,
				GameTypeName = gameType != null ? gameType.Name : null
			};

		var oneOffEvents =
			from oneOffEvent in context.LeagueOneOffEvents.AsNoTracking()
			where oneOffEvent.LeagueId == request.LeagueId
				&& oneOffEvent.LaunchedGameId.HasValue
				&& oneOffEvent.Status != Data.Entities.LeagueOneOffEventStatus.Completed
				&& oneOffEvent.Status != Data.Entities.LeagueOneOffEventStatus.Canceled
			join game in context.Games.AsNoTracking() on (oneOffEvent.LaunchedGameId ?? Guid.Empty) equals game.Id into games
			from game in games.DefaultIfEmpty()
			where game == null || game.Status != Data.Entities.GameStatus.Completed
			join gameType in context.GameTypes.AsNoTracking() on game.GameTypeId equals gameType.Id into gameTypes
			from gameType in gameTypes.DefaultIfEmpty()
			select new LeagueActiveGameEntryDto
			{
				GameId = oneOffEvent.LaunchedGameId ?? Guid.Empty,
				Name = oneOffEvent.Name,
				EventType = oneOffEvent.EventType == Data.Entities.LeagueOneOffEventType.Tournament ? "Tournament" : "Cash Game",
				StartedAt = game != null ? game.StartedAt ?? game.CreatedAt : oneOffEvent.ScheduledAtUtc,
				CurrentPhase = game != null ? game.CurrentPhase : null,
				PlayerCount = game != null
					? context.GamePlayers.Count(player => player.GameId == game.Id && player.Status == Data.Entities.GamePlayerStatus.Active)
					: 0,
				CreatedByName = game != null ? game.CreatedByName ?? game.CreatedById ?? oneOffEvent.CreatedByUserId : oneOffEvent.CreatedByUserId,
				GameTypeName = gameType != null ? gameType.Name : oneOffEvent.GameTypeCode
			};

		var query = seasonEvents.Concat(oneOffEvents);
		var totalCount = await query.CountAsync(cancellationToken);
		var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

		if (pageNumber > totalPages)
		{
			pageNumber = totalPages;
		}

		var skip = (pageNumber - 1) * pageSize;
		var entries = await query
			.OrderByDescending(x => x.StartedAt ?? DateTimeOffset.MinValue)
			.ThenBy(x => x.Name)
			.Skip(skip)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		return new LeagueActiveGamesPageDto
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