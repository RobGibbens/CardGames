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
using System.Globalization;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.UpdateLeagueSeasonEvent;

public sealed class UpdateLeagueSeasonEventCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	ILeagueBroadcaster leagueBroadcaster)
	: IRequestHandler<UpdateLeagueSeasonEventCommand, OneOf<Unit, UpdateLeagueSeasonEventError>>
{
	public async Task<OneOf<Unit, UpdateLeagueSeasonEventError>> Handle(UpdateLeagueSeasonEventCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.Unauthorized, "User is not authenticated.");
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

		if (seasonEvent.Status != Data.Entities.LeagueSeasonEventStatus.Planned || seasonEvent.LaunchedGameId.HasValue)
		{
			return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.Conflict, "Only planned, unlaunched season events can be edited.");
		}

		if (request.Request.ScheduledAtUtc == default)
		{
			return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.InvalidRequest, "Scheduled date/time is required.");
		}

		if (!LeagueEventSchedulingGuard.IsScheduledAtInFuture(request.Request.ScheduledAtUtc))
		{
			return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.InvalidRequest, LeagueEventSchedulingGuard.ScheduledAtUtcMustBeInFutureMessage);
		}

		string? normalizedGameTypeCode = null;
		CardGames.Poker.Games.GameFlow.GameRules? rules = null;
		if (!string.IsNullOrWhiteSpace(request.Request.GameTypeCode))
		{
			if (!LeagueEventGameSettings.TryResolve(request.Request.GameTypeCode, out var resolvedGameTypeCode, out rules, out var gameTypeError))
			{
				return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.InvalidRequest, gameTypeError!);
			}

			var stakesError = LeagueEventGameSettings.Validate(rules!, request.Request.Ante, request.Request.MinBet, request.Request.SmallBlind, request.Request.BigBlind);
			if (stakesError is not null)
			{
				return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.InvalidRequest, stakesError);
			}

			normalizedGameTypeCode = resolvedGameTypeCode;
		}

		var tournamentBuyInError = ValidateSeasonTournamentBuyIn(request.Request.TournamentBuyIn);
		if (tournamentBuyInError is not null)
		{
			return new UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode.InvalidRequest, tournamentBuyInError);
		}

		seasonEvent.Name = GenerateSeasonEventName(request.Request.ScheduledAtUtc);
		seasonEvent.SequenceNumber = null;
		seasonEvent.ScheduledAtUtc = request.Request.ScheduledAtUtc;
		seasonEvent.Notes = string.IsNullOrWhiteSpace(request.Request.Notes) ? null : request.Request.Notes.Trim();
		seasonEvent.GameTypeCode = normalizedGameTypeCode;
		seasonEvent.Ante = rules is null ? null : LeagueEventGameSettings.NormalizeAnte(rules, request.Request.Ante);
		seasonEvent.MinBet = rules is null ? null : LeagueEventGameSettings.NormalizeMinBet(rules, request.Request.MinBet, request.Request.BigBlind);
		seasonEvent.SmallBlind = rules is null ? null : LeagueEventGameSettings.NormalizeSmallBlind(rules, request.Request.MinBet, request.Request.SmallBlind, request.Request.BigBlind);
		seasonEvent.BigBlind = rules is null ? null : LeagueEventGameSettings.NormalizeBigBlind(rules, request.Request.MinBet, request.Request.BigBlind);
		seasonEvent.TournamentBuyIn = request.Request.TournamentBuyIn;

		await context.SaveChangesAsync(cancellationToken);
		await leagueBroadcaster.BroadcastLeagueEventChangedAsync(new CardGames.Contracts.SignalR.LeagueEventChangedDto
		{
			LeagueId = seasonEvent.LeagueId,
			EventId = seasonEvent.Id,
			SourceType = CardGames.Contracts.SignalR.LeagueEventSourceType.Season,
			SeasonId = seasonEvent.LeagueSeasonId,
			ChangeKind = CardGames.Contracts.SignalR.LeagueEventChangeKind.Updated,
			ChangedAtUtc = DateTimeOffset.UtcNow
		}, cancellationToken);

		return Unit.Value;
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

	private static string GenerateSeasonEventName(DateTimeOffset scheduledAtUtc)
	{
		return scheduledAtUtc.ToUniversalTime().ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture);
	}
}