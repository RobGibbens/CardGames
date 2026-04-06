using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.UpdateLeagueOneOffEvent;

public sealed class UpdateLeagueOneOffEventCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<UpdateLeagueOneOffEventCommand, OneOf<Unit, UpdateLeagueOneOffEventError>>
{
	public async Task<OneOf<Unit, UpdateLeagueOneOffEventError>> Handle(UpdateLeagueOneOffEventCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new UpdateLeagueOneOffEventError(UpdateLeagueOneOffEventErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (string.IsNullOrWhiteSpace(request.Request.Name))
		{
			return new UpdateLeagueOneOffEventError(UpdateLeagueOneOffEventErrorCode.InvalidRequest, "One-off event name is required.");
		}

		if (request.Request.ScheduledAtUtc == default)
		{
			return new UpdateLeagueOneOffEventError(UpdateLeagueOneOffEventErrorCode.InvalidRequest, "Scheduled date/time is required.");
		}

		if (string.IsNullOrWhiteSpace(request.Request.GameTypeCode))
		{
			return new UpdateLeagueOneOffEventError(UpdateLeagueOneOffEventErrorCode.InvalidRequest, "Game variant is required.");
		}

		var leagueExists = await context.Leagues
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.LeagueId, cancellationToken);

		if (!leagueExists)
		{
			return new UpdateLeagueOneOffEventError(UpdateLeagueOneOffEventErrorCode.LeagueNotFound, "League not found.");
		}

		var canManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!canManageLeague)
		{
			return new UpdateLeagueOneOffEventError(UpdateLeagueOneOffEventErrorCode.Forbidden, "Only league managers or admins can update one-off events.");
		}

		var oneOffEvent = await context.LeagueOneOffEvents
			.FirstOrDefaultAsync(x => x.Id == request.EventId && x.LeagueId == request.LeagueId, cancellationToken);

		if (oneOffEvent is null)
		{
			return new UpdateLeagueOneOffEventError(UpdateLeagueOneOffEventErrorCode.EventNotFound, "One-off event not found in league.");
		}

		if (oneOffEvent.Status != LeagueOneOffEventStatus.Planned || oneOffEvent.LaunchedGameId.HasValue)
		{
			return new UpdateLeagueOneOffEventError(UpdateLeagueOneOffEventErrorCode.Conflict, "Only planned, unlaunched one-off events can be edited.");
		}

		oneOffEvent.Name = request.Request.Name.Trim();
		oneOffEvent.ScheduledAtUtc = request.Request.ScheduledAtUtc;
		oneOffEvent.EventType = (LeagueOneOffEventType)request.Request.EventType;
		oneOffEvent.Notes = string.IsNullOrWhiteSpace(request.Request.Notes) ? null : request.Request.Notes.Trim();
		oneOffEvent.GameTypeCode = request.Request.GameTypeCode.Trim();
		oneOffEvent.TableName = string.IsNullOrWhiteSpace(request.Request.TableName) ? null : request.Request.TableName.Trim();
		oneOffEvent.Ante = request.Request.Ante;
		oneOffEvent.MinBet = request.Request.MinBet;

		await context.SaveChangesAsync(cancellationToken);

		return Unit.Value;
	}
}