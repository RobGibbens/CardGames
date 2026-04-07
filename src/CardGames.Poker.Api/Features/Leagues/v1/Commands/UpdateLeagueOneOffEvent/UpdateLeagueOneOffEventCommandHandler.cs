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

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.UpdateLeagueOneOffEvent;

public sealed class UpdateLeagueOneOffEventCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	ILeagueBroadcaster leagueBroadcaster)
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

		if (!LeagueEventSchedulingGuard.IsScheduledAtInFuture(request.Request.ScheduledAtUtc))
		{
			return new UpdateLeagueOneOffEventError(UpdateLeagueOneOffEventErrorCode.InvalidRequest, LeagueEventSchedulingGuard.ScheduledAtUtcMustBeInFutureMessage);
		}

		if (string.IsNullOrWhiteSpace(request.Request.GameTypeCode))
		{
			return new UpdateLeagueOneOffEventError(UpdateLeagueOneOffEventErrorCode.InvalidRequest, "Game variant is required.");
		}

		var tournamentBuyInError = ValidateOneOffTournamentBuyIn(request.Request.EventType, request.Request.TournamentBuyIn);
		if (tournamentBuyInError is not null)
		{
			return new UpdateLeagueOneOffEventError(UpdateLeagueOneOffEventErrorCode.InvalidRequest, tournamentBuyInError);
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

		if (oneOffEvent.Status != Data.Entities.LeagueOneOffEventStatus.Planned || oneOffEvent.LaunchedGameId.HasValue)
		{
			return new UpdateLeagueOneOffEventError(UpdateLeagueOneOffEventErrorCode.Conflict, "Only planned, unlaunched one-off events can be edited.");
		}

		oneOffEvent.Name = request.Request.Name.Trim();
		oneOffEvent.ScheduledAtUtc = request.Request.ScheduledAtUtc;
		oneOffEvent.EventType = (Data.Entities.LeagueOneOffEventType)request.Request.EventType;
		oneOffEvent.Notes = string.IsNullOrWhiteSpace(request.Request.Notes) ? null : request.Request.Notes.Trim();
		oneOffEvent.GameTypeCode = request.Request.GameTypeCode.Trim();
		oneOffEvent.Ante = request.Request.Ante;
		oneOffEvent.MinBet = request.Request.MinBet;
		oneOffEvent.TournamentBuyIn = request.Request.TournamentBuyIn;

		await context.SaveChangesAsync(cancellationToken);
		await leagueBroadcaster.BroadcastLeagueEventChangedAsync(new CardGames.Contracts.SignalR.LeagueEventChangedDto
		{
			LeagueId = oneOffEvent.LeagueId,
			EventId = oneOffEvent.Id,
			SourceType = CardGames.Contracts.SignalR.LeagueEventSourceType.OneOff,
			SeasonId = null,
			ChangeKind = CardGames.Contracts.SignalR.LeagueEventChangeKind.Updated,
			ChangedAtUtc = DateTimeOffset.UtcNow
		}, cancellationToken);

		return Unit.Value;
	}

	private static string? ValidateOneOffTournamentBuyIn(CardGames.Poker.Api.Contracts.LeagueOneOffEventType eventType, int? tournamentBuyIn)
	{
		if (eventType == CardGames.Poker.Api.Contracts.LeagueOneOffEventType.Tournament)
		{
			if (!tournamentBuyIn.HasValue)
			{
				return "Tournament buy-in is required for league tournaments.";
			}

			if (tournamentBuyIn.Value <= 0 || tournamentBuyIn.Value > LeagueEventBuyInRules.MaxTournamentBuyIn)
			{
				return $"Tournament buy-in must be between 1 and {LeagueEventBuyInRules.MaxTournamentBuyIn:N0}.";
			}

			return null;
		}

		if (tournamentBuyIn.HasValue)
		{
			return "Tournament buy-in can only be set for tournament events.";
		}

		return null;
	}
}