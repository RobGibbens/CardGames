using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeasonEvent;

public sealed class CreateLeagueSeasonEventCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	ILeagueBroadcaster leagueBroadcaster)
	: IRequestHandler<CreateLeagueSeasonEventCommand, OneOf<CreateLeagueSeasonEventResponse, CreateLeagueSeasonEventError>>
{
	public async Task<OneOf<CreateLeagueSeasonEventResponse, CreateLeagueSeasonEventError>> Handle(CreateLeagueSeasonEventCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new CreateLeagueSeasonEventError(CreateLeagueSeasonEventErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (string.IsNullOrWhiteSpace(request.Request.Name))
		{
			return new CreateLeagueSeasonEventError(CreateLeagueSeasonEventErrorCode.InvalidRequest, "Event name is required.");
		}

		if (request.Request.SequenceNumber.HasValue && request.Request.SequenceNumber.Value <= 0)
		{
			return new CreateLeagueSeasonEventError(CreateLeagueSeasonEventErrorCode.InvalidRequest, "Sequence number must be greater than zero when provided.");
		}

		var leagueExists = await context.Leagues
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.LeagueId, cancellationToken);

		if (!leagueExists)
		{
			return new CreateLeagueSeasonEventError(CreateLeagueSeasonEventErrorCode.LeagueNotFound, "League not found.");
		}

		var season = await context.LeagueSeasons
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == request.SeasonId && x.LeagueId == request.LeagueId, cancellationToken);

		if (season is null)
		{
			return new CreateLeagueSeasonEventError(CreateLeagueSeasonEventErrorCode.SeasonNotFound, "Season not found in league.");
		}

		var canManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!canManageLeague)
		{
			return new CreateLeagueSeasonEventError(CreateLeagueSeasonEventErrorCode.Forbidden, "Only league managers or admins can create season events.");
		}

		if (request.Request.ScheduledAtUtc == default)
		{
			return new CreateLeagueSeasonEventError(CreateLeagueSeasonEventErrorCode.InvalidRequest, "Scheduled date/time is required.");
		}

		if (!LeagueEventSchedulingGuard.IsScheduledAtInFuture(request.Request.ScheduledAtUtc))
		{
			return new CreateLeagueSeasonEventError(CreateLeagueSeasonEventErrorCode.InvalidRequest, LeagueEventSchedulingGuard.ScheduledAtUtcMustBeInFutureMessage);
		}

		var tournamentBuyInError = ValidateSeasonTournamentBuyIn(request.Request.TournamentBuyIn);
		if (tournamentBuyInError is not null)
		{
			return new CreateLeagueSeasonEventError(CreateLeagueSeasonEventErrorCode.InvalidRequest, tournamentBuyInError);
		}

		if (request.Request.SequenceNumber.HasValue)
		{
			var sequenceInUse = await context.LeagueSeasonEvents
				.AsNoTracking()
				.AnyAsync(x => x.LeagueSeasonId == request.SeasonId && x.SequenceNumber == request.Request.SequenceNumber, cancellationToken);

			if (sequenceInUse)
			{
				return new CreateLeagueSeasonEventError(CreateLeagueSeasonEventErrorCode.InvalidRequest, "Sequence number already exists for this season.");
			}
		}

		var seasonEvent = new LeagueSeasonEvent
		{
			Id = Guid.CreateVersion7(),
			LeagueId = request.LeagueId,
			LeagueSeasonId = request.SeasonId,
			Name = request.Request.Name.Trim(),
			SequenceNumber = request.Request.SequenceNumber,
			ScheduledAtUtc = request.Request.ScheduledAtUtc,
			Status = Data.Entities.LeagueSeasonEventStatus.Planned,
			Notes = string.IsNullOrWhiteSpace(request.Request.Notes) ? null : request.Request.Notes.Trim(),
			TournamentBuyIn = request.Request.TournamentBuyIn,
			CreatedByUserId = currentUserService.UserId,
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		context.LeagueSeasonEvents.Add(seasonEvent);
		await context.SaveChangesAsync(cancellationToken);
		await leagueBroadcaster.BroadcastLeagueEventChangedAsync(new CardGames.Contracts.SignalR.LeagueEventChangedDto
		{
			LeagueId = seasonEvent.LeagueId,
			EventId = seasonEvent.Id,
			SourceType = CardGames.Contracts.SignalR.LeagueEventSourceType.Season,
			SeasonId = seasonEvent.LeagueSeasonId,
			ChangeKind = CardGames.Contracts.SignalR.LeagueEventChangeKind.Created,
			ChangedAtUtc = seasonEvent.CreatedAtUtc
		}, cancellationToken);

		return new CreateLeagueSeasonEventResponse
		{
			EventId = seasonEvent.Id,
			LeagueId = seasonEvent.LeagueId,
			SeasonId = seasonEvent.LeagueSeasonId,
			Name = seasonEvent.Name,
			SequenceNumber = seasonEvent.SequenceNumber,
			ScheduledAtUtc = seasonEvent.ScheduledAtUtc,
			Status = (Contracts.LeagueSeasonEventStatus)seasonEvent.Status,
			Notes = seasonEvent.Notes,
			CreatedByUserId = seasonEvent.CreatedByUserId,
			CreatedAtUtc = seasonEvent.CreatedAtUtc,
			TournamentBuyIn = seasonEvent.TournamentBuyIn
		};
	}

	private static string? ValidateSeasonTournamentBuyIn(int? tournamentBuyIn)
	{
		if (!tournamentBuyIn.HasValue)
		{
			return null;
		}

		if (tournamentBuyIn.Value <= 0 || tournamentBuyIn.Value > LeagueEventBuyInRules.MaxTournamentBuyIn)
		{
			return $"Tournament buy-in must be between 1 and {LeagueEventBuyInRules.MaxTournamentBuyIn:N0}.";
		}

		return null;
	}
}