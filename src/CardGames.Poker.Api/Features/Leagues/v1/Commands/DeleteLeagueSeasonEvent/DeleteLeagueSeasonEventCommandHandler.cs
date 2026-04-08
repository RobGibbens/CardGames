using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.DeleteLeagueSeasonEvent;

public sealed class DeleteLeagueSeasonEventCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	ILeagueBroadcaster leagueBroadcaster)
	: IRequestHandler<DeleteLeagueSeasonEventCommand, OneOf<Unit, DeleteLeagueSeasonEventError>>
{
	public async Task<OneOf<Unit, DeleteLeagueSeasonEventError>> Handle(DeleteLeagueSeasonEventCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new DeleteLeagueSeasonEventError(DeleteLeagueSeasonEventErrorCode.Unauthorized, "User is not authenticated.");
		}

		var leagueExists = await context.Leagues
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.LeagueId, cancellationToken);

		if (!leagueExists)
		{
			return new DeleteLeagueSeasonEventError(DeleteLeagueSeasonEventErrorCode.LeagueNotFound, "League not found.");
		}

		var seasonExists = await context.LeagueSeasons
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.SeasonId && x.LeagueId == request.LeagueId, cancellationToken);

		if (!seasonExists)
		{
			return new DeleteLeagueSeasonEventError(DeleteLeagueSeasonEventErrorCode.SeasonNotFound, "Season not found in league.");
		}

		var canManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!canManageLeague)
		{
			return new DeleteLeagueSeasonEventError(DeleteLeagueSeasonEventErrorCode.Forbidden, "Only league managers or admins can delete season events.");
		}

		var seasonEvent = await context.LeagueSeasonEvents
			.FirstOrDefaultAsync(x => x.Id == request.EventId && x.LeagueId == request.LeagueId && x.LeagueSeasonId == request.SeasonId, cancellationToken);

		if (seasonEvent is null)
		{
			return new DeleteLeagueSeasonEventError(DeleteLeagueSeasonEventErrorCode.EventNotFound, "Season event not found in league season.");
		}

		if (seasonEvent.Status != LeagueSeasonEventStatus.Planned || seasonEvent.LaunchedGameId.HasValue)
		{
			return new DeleteLeagueSeasonEventError(DeleteLeagueSeasonEventErrorCode.Conflict, "Only planned, unlaunched season events can be deleted.");
		}

		var seasonEventId = seasonEvent.Id;
		var seasonId = seasonEvent.LeagueSeasonId;
		context.LeagueSeasonEvents.Remove(seasonEvent);
		await context.SaveChangesAsync(cancellationToken);
		await leagueBroadcaster.BroadcastLeagueEventChangedAsync(new CardGames.Contracts.SignalR.LeagueEventChangedDto
		{
			LeagueId = request.LeagueId,
			EventId = seasonEventId,
			SourceType = CardGames.Contracts.SignalR.LeagueEventSourceType.Season,
			SeasonId = seasonId,
			ChangeKind = CardGames.Contracts.SignalR.LeagueEventChangeKind.Deleted,
			ChangedAtUtc = DateTimeOffset.UtcNow
		}, cancellationToken);

		return Unit.Value;
	}
}