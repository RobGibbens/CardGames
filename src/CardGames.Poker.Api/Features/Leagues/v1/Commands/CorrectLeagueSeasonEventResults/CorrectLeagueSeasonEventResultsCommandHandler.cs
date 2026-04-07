using System.Text.Json;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CorrectLeagueSeasonEventResults;

public sealed class CorrectLeagueSeasonEventResultsCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	ILeagueBroadcaster leagueBroadcaster)
	: IRequestHandler<CorrectLeagueSeasonEventResultsCommand, OneOf<Unit, CorrectLeagueSeasonEventResultsError>>
{
	public async Task<OneOf<Unit, CorrectLeagueSeasonEventResultsError>> Handle(CorrectLeagueSeasonEventResultsCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (string.IsNullOrWhiteSpace(request.Request.Reason))
		{
			return new CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode.InvalidRequest, "Correction reason is required.");
		}

		if (request.Request.Results is null || request.Request.Results.Count == 0)
		{
			return new CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode.InvalidRequest, "At least one corrected result is required.");
		}

		var actorCanManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!actorCanManageLeague)
		{
			return new CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode.Forbidden, "Only league managers or admins can correct results.");
		}

		var leagueExists = await context.Leagues
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.LeagueId, cancellationToken);

		if (!leagueExists)
		{
			return new CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode.LeagueNotFound, "League not found.");
		}

		var season = await context.LeagueSeasons
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == request.SeasonId, cancellationToken);

		if (season is null)
		{
			return new CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode.SeasonNotFound, "Season not found.");
		}

		if (season.LeagueId != request.LeagueId)
		{
			return new CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode.MismatchedLeagueOrSeason, "Season does not belong to the requested league.");
		}

		var seasonEvent = await context.LeagueSeasonEvents
			.FirstOrDefaultAsync(x => x.Id == request.EventId, cancellationToken);

		if (seasonEvent is null)
		{
			return new CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode.EventNotFound, "Season event not found.");
		}

		if (seasonEvent.LeagueId != request.LeagueId || seasonEvent.LeagueSeasonId != request.SeasonId)
		{
			return new CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode.MismatchedLeagueOrSeason, "Season event does not belong to the requested league season.");
		}

		var duplicateMemberIds = request.Request.Results
			.GroupBy(x => x.MemberUserId, StringComparer.Ordinal)
			.Where(x => x.Count() > 1)
			.Select(x => x.Key)
			.ToList();

		if (duplicateMemberIds.Count > 0)
		{
			return new CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode.InvalidRequest, "Each member can appear only once in event results.");
		}

		if (request.Request.Results.Any(x => string.IsNullOrWhiteSpace(x.MemberUserId) || x.Placement <= 0 || x.Points < 0))
		{
			return new CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode.InvalidRequest, "Result entries must include a member id, positive placement, and non-negative points.");
		}

		var memberIds = request.Request.Results
			.Select(x => x.MemberUserId.Trim())
			.ToArray();

		var activeMemberIds = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.IsActive && memberIds.Contains(x.UserId))
			.Select(x => x.UserId)
			.ToListAsync(cancellationToken);

		if (activeMemberIds.Count != memberIds.Length)
		{
			return new CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode.MemberNotFound, "All result entries must reference active league members.");
		}

		var existingResults = await context.LeagueSeasonEventResults
			.Where(x => x.LeagueSeasonEventId == request.EventId)
			.ToListAsync(cancellationToken);

		if (existingResults.Count == 0)
		{
			return new CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode.ResultsNotIngested, "Cannot correct results before initial ingestion.");
		}

		var existingNormalized = existingResults
			.Select(x => new
			{
				x.UserId,
				x.Placement,
				x.Points,
				x.ChipsDelta
			})
			.OrderBy(x => x.UserId, StringComparer.Ordinal)
			.ThenBy(x => x.Placement)
			.ThenBy(x => x.Points)
			.ThenBy(x => x.ChipsDelta)
			.ToArray();

		var incomingNormalized = request.Request.Results
			.Select(x => new
			{
				UserId = x.MemberUserId.Trim(),
				x.Placement,
				x.Points,
				x.ChipsDelta
			})
			.OrderBy(x => x.UserId, StringComparer.Ordinal)
			.ThenBy(x => x.Placement)
			.ThenBy(x => x.Points)
			.ThenBy(x => x.ChipsDelta)
			.ToArray();

		if (existingNormalized.SequenceEqual(incomingNormalized))
		{
			return Unit.Value;
		}

		var previousSnapshotJson = JsonSerializer.Serialize(existingNormalized);
		var newSnapshotJson = JsonSerializer.Serialize(incomingNormalized);
		var now = DateTimeOffset.UtcNow;

		context.LeagueSeasonEventResults.RemoveRange(existingResults);
		foreach (var result in request.Request.Results)
		{
			context.LeagueSeasonEventResults.Add(new LeagueSeasonEventResult
			{
				LeagueId = request.LeagueId,
				LeagueSeasonId = request.SeasonId,
				LeagueSeasonEventId = request.EventId,
				UserId = result.MemberUserId.Trim(),
				Placement = result.Placement,
				Points = result.Points,
				ChipsDelta = result.ChipsDelta,
				RecordedByUserId = currentUserService.UserId,
				RecordedAtUtc = now
			});
		}

		context.LeagueSeasonEventResultCorrectionAudits.Add(new LeagueSeasonEventResultCorrectionAudit
		{
			Id = Guid.CreateVersion7(),
			LeagueId = request.LeagueId,
			LeagueSeasonId = request.SeasonId,
			LeagueSeasonEventId = request.EventId,
			CorrectedByUserId = currentUserService.UserId,
			Reason = request.Request.Reason.Trim(),
			PreviousResultsSnapshotJson = previousSnapshotJson,
			NewResultsSnapshotJson = newSnapshotJson,
			CorrectedAtUtc = now
		});

		seasonEvent.Status = LeagueSeasonEventStatus.Completed;

		await context.SaveChangesAsync(cancellationToken);

		var affectedMemberIds = existingResults
			.Select(x => x.UserId)
			.Concat(memberIds)
			.Distinct(StringComparer.Ordinal)
			.ToArray();

		await LeagueSeasonEventStandingsRecalculator.RebuildForMembersAsync(context, request.LeagueId, affectedMemberIds, cancellationToken);
		await context.SaveChangesAsync(cancellationToken);
		await leagueBroadcaster.BroadcastLeagueEventChangedAsync(new CardGames.Contracts.SignalR.LeagueEventChangedDto
		{
			LeagueId = request.LeagueId,
			EventId = request.EventId,
			SourceType = CardGames.Contracts.SignalR.LeagueEventSourceType.Season,
			SeasonId = request.SeasonId,
			ChangeKind = CardGames.Contracts.SignalR.LeagueEventChangeKind.ResultsRecorded,
			ChangedAtUtc = now
		}, cancellationToken);

		return Unit.Value;
	}
}
