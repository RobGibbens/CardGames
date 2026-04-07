using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.DeleteLeagueOneOffEvent;

public sealed class DeleteLeagueOneOffEventCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	ILeagueBroadcaster leagueBroadcaster)
	: IRequestHandler<DeleteLeagueOneOffEventCommand, OneOf<Unit, DeleteLeagueOneOffEventError>>
{
	public async Task<OneOf<Unit, DeleteLeagueOneOffEventError>> Handle(DeleteLeagueOneOffEventCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new DeleteLeagueOneOffEventError(DeleteLeagueOneOffEventErrorCode.Unauthorized, "User is not authenticated.");
		}

		var leagueExists = await context.Leagues
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.LeagueId, cancellationToken);

		if (!leagueExists)
		{
			return new DeleteLeagueOneOffEventError(DeleteLeagueOneOffEventErrorCode.LeagueNotFound, "League not found.");
		}

		var canManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!canManageLeague)
		{
			return new DeleteLeagueOneOffEventError(DeleteLeagueOneOffEventErrorCode.Forbidden, "Only league managers or admins can delete one-off events.");
		}

		var oneOffEvent = await context.LeagueOneOffEvents
			.FirstOrDefaultAsync(x => x.Id == request.EventId && x.LeagueId == request.LeagueId, cancellationToken);

		if (oneOffEvent is null)
		{
			return new DeleteLeagueOneOffEventError(DeleteLeagueOneOffEventErrorCode.EventNotFound, "One-off event not found in league.");
		}

		if (oneOffEvent.Status != LeagueOneOffEventStatus.Planned || oneOffEvent.LaunchedGameId.HasValue)
		{
			return new DeleteLeagueOneOffEventError(DeleteLeagueOneOffEventErrorCode.Conflict, "Only planned, unlaunched one-off events can be deleted.");
		}

		var eventId = oneOffEvent.Id;
		context.LeagueOneOffEvents.Remove(oneOffEvent);
		await context.SaveChangesAsync(cancellationToken);
		await leagueBroadcaster.BroadcastLeagueEventChangedAsync(new CardGames.Contracts.SignalR.LeagueEventChangedDto
		{
			LeagueId = request.LeagueId,
			EventId = eventId,
			SourceType = CardGames.Contracts.SignalR.LeagueEventSourceType.OneOff,
			SeasonId = null,
			ChangeKind = CardGames.Contracts.SignalR.LeagueEventChangeKind.Deleted,
			ChangedAtUtc = DateTimeOffset.UtcNow
		}, cancellationToken);

		return Unit.Value;
	}
}