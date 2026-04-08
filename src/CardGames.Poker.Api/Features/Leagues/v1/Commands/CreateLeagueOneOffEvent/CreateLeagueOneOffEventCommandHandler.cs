using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands;
using CardGames.Poker.Api.Features.Leagues.v1;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueOneOffEvent;

public sealed class CreateLeagueOneOffEventCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	ILeagueBroadcaster leagueBroadcaster)
	: IRequestHandler<CreateLeagueOneOffEventCommand, OneOf<CreateLeagueOneOffEventResponse, CreateLeagueOneOffEventError>>
{
	public async Task<OneOf<CreateLeagueOneOffEventResponse, CreateLeagueOneOffEventError>> Handle(CreateLeagueOneOffEventCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (string.IsNullOrWhiteSpace(request.Request.Name))
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.InvalidRequest, "One-off event name is required.");
		}

		if (request.Request.ScheduledAtUtc == default)
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.InvalidRequest, "Scheduled date/time is required.");
		}

		if (!LeagueEventGameSettings.TryResolve(request.Request.GameTypeCode, out var normalizedGameTypeCode, out var rules, out var gameTypeError))
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.InvalidRequest, gameTypeError!);
		}

		var stakesError = LeagueEventGameSettings.Validate(rules!, request.Request.Ante, request.Request.MinBet, request.Request.SmallBlind, request.Request.BigBlind);
		if (stakesError is not null)
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.InvalidRequest, stakesError);
		}

		if (!LeagueEventSchedulingGuard.IsScheduledAtInFuture(request.Request.ScheduledAtUtc))
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.InvalidRequest, LeagueEventSchedulingGuard.ScheduledAtUtcMustBeInFutureMessage);
		}

		var tournamentBuyInError = ValidateOneOffTournamentBuyIn(request.Request.EventType, request.Request.TournamentBuyIn);
		if (tournamentBuyInError is not null)
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.InvalidRequest, tournamentBuyInError);
		}

		var leagueExists = await context.Leagues
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.LeagueId, cancellationToken);

		if (!leagueExists)
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.LeagueNotFound, "League not found.");
		}

		var canManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!canManageLeague)
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.Forbidden, "Only league managers or admins can create one-off events.");
		}

		var oneOffEvent = new LeagueOneOffEvent
		{
			Id = Guid.CreateVersion7(),
			LeagueId = request.LeagueId,
			Name = request.Request.Name.Trim(),
			ScheduledAtUtc = request.Request.ScheduledAtUtc,
			EventType = (Data.Entities.LeagueOneOffEventType)request.Request.EventType,
			Status = Data.Entities.LeagueOneOffEventStatus.Planned,
			Notes = string.IsNullOrWhiteSpace(request.Request.Notes) ? null : request.Request.Notes.Trim(),
			GameTypeCode = normalizedGameTypeCode,
			Ante = LeagueEventGameSettings.NormalizeAnte(rules!, request.Request.Ante),
			MinBet = LeagueEventGameSettings.NormalizeMinBet(rules!, request.Request.MinBet, request.Request.BigBlind),
			SmallBlind = LeagueEventGameSettings.NormalizeSmallBlind(rules!, request.Request.MinBet, request.Request.SmallBlind, request.Request.BigBlind),
			BigBlind = LeagueEventGameSettings.NormalizeBigBlind(rules!, request.Request.MinBet, request.Request.BigBlind),
			TournamentBuyIn = request.Request.TournamentBuyIn,
			CreatedByUserId = currentUserService.UserId,
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		context.LeagueOneOffEvents.Add(oneOffEvent);
		await context.SaveChangesAsync(cancellationToken);
		await leagueBroadcaster.BroadcastLeagueEventChangedAsync(new CardGames.Contracts.SignalR.LeagueEventChangedDto
		{
			LeagueId = oneOffEvent.LeagueId,
			EventId = oneOffEvent.Id,
			SourceType = CardGames.Contracts.SignalR.LeagueEventSourceType.OneOff,
			SeasonId = null,
			ChangeKind = CardGames.Contracts.SignalR.LeagueEventChangeKind.Created,
			ChangedAtUtc = oneOffEvent.CreatedAtUtc
		}, cancellationToken);

		return new CreateLeagueOneOffEventResponse
		{
			EventId = oneOffEvent.Id,
			LeagueId = oneOffEvent.LeagueId,
			Name = oneOffEvent.Name,
			ScheduledAtUtc = oneOffEvent.ScheduledAtUtc,
			EventType = request.Request.EventType,
			Status = (Contracts.LeagueOneOffEventStatus)oneOffEvent.Status,
			Notes = oneOffEvent.Notes,
			CreatedByUserId = oneOffEvent.CreatedByUserId,
			CreatedAtUtc = oneOffEvent.CreatedAtUtc,
			GameTypeCode = oneOffEvent.GameTypeCode,
			Ante = oneOffEvent.Ante,
			MinBet = oneOffEvent.MinBet,
			SmallBlind = oneOffEvent.SmallBlind,
			BigBlind = oneOffEvent.BigBlind,
			TournamentBuyIn = oneOffEvent.TournamentBuyIn
		};
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