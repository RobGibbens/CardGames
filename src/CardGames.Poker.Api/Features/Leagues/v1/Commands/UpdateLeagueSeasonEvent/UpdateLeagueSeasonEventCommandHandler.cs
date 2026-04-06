using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.UpdateLeagueSeasonEvent;

public sealed class UpdateLeagueSeasonEventCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<UpdateLeagueSeasonEventCommand, OneOf<Unit, UpdateLeagueSeasonEventError>>
{
	public async Task<OneOf<Unit, UpdateLeagueSeasonEventError>> Handle(UpdateLeagueSeasonEventCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (string.IsNullOrWhiteSpace(request.Request.Name))
		{
			return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.InvalidRequest, "Event name is required.");
		}

		if (request.Request.SequenceNumber.HasValue && request.Request.SequenceNumber.Value <= 0)
		{
			return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.InvalidRequest, "Sequence number must be greater than zero when provided.");
		}

		var leagueExists = await context.Leagues
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.LeagueId, cancellationToken);

		if (!leagueExists)
		{
			return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.LeagueNotFound, "League not found.");
		}

		var season = await context.LeagueSeasons
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == request.SeasonId && x.LeagueId == request.LeagueId, cancellationToken);

		if (season is null)
		{
			return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.SeasonNotFound, "Season not found in league.");
		}

		var canManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!canManageLeague)
		{
			return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.Forbidden, "Only league managers or admins can update season events.");
		}

		var seasonEvent = await context.LeagueSeasonEvents
			.FirstOrDefaultAsync(x => x.Id == request.EventId && x.LeagueId == request.LeagueId && x.LeagueSeasonId == request.SeasonId, cancellationToken);

		if (seasonEvent is null)
		{
			return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.EventNotFound, "Season event not found in league season.");
		}

		if (seasonEvent.Status != LeagueSeasonEventStatus.Planned || seasonEvent.LaunchedGameId.HasValue)
		{
			return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.Conflict, "Only planned, unlaunched season events can be edited.");
		}

		if (request.Request.SequenceNumber.HasValue)
		{
			var sequenceInUse = await context.LeagueSeasonEvents
				.AsNoTracking()
				.AnyAsync(
					x => x.LeagueSeasonId == request.SeasonId
						&& x.Id != request.EventId
						&& x.SequenceNumber == request.Request.SequenceNumber,
					cancellationToken);

			if (sequenceInUse)
			{
				return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.InvalidRequest, "Sequence number already exists for this season.");
			}
		}

		seasonEvent.Name = request.Request.Name.Trim();
		seasonEvent.SequenceNumber = request.Request.SequenceNumber;
		seasonEvent.ScheduledAtUtc = request.Request.ScheduledAtUtc;
		seasonEvent.Notes = string.IsNullOrWhiteSpace(request.Request.Notes) ? null : request.Request.Notes.Trim();

		await context.SaveChangesAsync(cancellationToken);

		return Unit.Value;
	}
}