using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;

public sealed class IngestLeagueSeasonEventResultsCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<IngestLeagueSeasonEventResultsCommand, OneOf<Unit, IngestLeagueSeasonEventResultsError>>
{
	public async Task<OneOf<Unit, IngestLeagueSeasonEventResultsError>> Handle(IngestLeagueSeasonEventResultsCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new IngestLeagueSeasonEventResultsError(IngestLeagueSeasonEventResultsErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (request.Request.Results is null || request.Request.Results.Count == 0)
		{
			return new IngestLeagueSeasonEventResultsError(IngestLeagueSeasonEventResultsErrorCode.InvalidRequest, "At least one result is required.");
		}

		var actorCanManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!actorCanManageLeague)
		{
			return new IngestLeagueSeasonEventResultsError(IngestLeagueSeasonEventResultsErrorCode.Forbidden, "Only league managers or admins can ingest results.");
		}

		var leagueExists = await context.Leagues
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.LeagueId, cancellationToken);

		if (!leagueExists)
		{
			return new IngestLeagueSeasonEventResultsError(IngestLeagueSeasonEventResultsErrorCode.LeagueNotFound, "League not found.");
		}

		var season = await context.LeagueSeasons
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == request.SeasonId, cancellationToken);

		if (season is null)
		{
			return new IngestLeagueSeasonEventResultsError(IngestLeagueSeasonEventResultsErrorCode.SeasonNotFound, "Season not found.");
		}

		if (season.LeagueId != request.LeagueId)
		{
			return new IngestLeagueSeasonEventResultsError(IngestLeagueSeasonEventResultsErrorCode.MismatchedLeagueOrSeason, "Season does not belong to the requested league.");
		}

		var seasonEvent = await context.LeagueSeasonEvents
			.FirstOrDefaultAsync(x => x.Id == request.EventId, cancellationToken);

		if (seasonEvent is null)
		{
			return new IngestLeagueSeasonEventResultsError(IngestLeagueSeasonEventResultsErrorCode.EventNotFound, "Season event not found.");
		}

		if (seasonEvent.LeagueId != request.LeagueId || seasonEvent.LeagueSeasonId != request.SeasonId)
		{
			return new IngestLeagueSeasonEventResultsError(IngestLeagueSeasonEventResultsErrorCode.MismatchedLeagueOrSeason, "Season event does not belong to the requested league season.");
		}

		var duplicateMemberIds = request.Request.Results
			.GroupBy(x => x.MemberUserId, StringComparer.Ordinal)
			.Where(x => x.Count() > 1)
			.Select(x => x.Key)
			.ToList();

		if (duplicateMemberIds.Count > 0)
		{
			return new IngestLeagueSeasonEventResultsError(IngestLeagueSeasonEventResultsErrorCode.InvalidRequest, "Each member can appear only once in event results.");
		}

		if (request.Request.Results.Any(x => string.IsNullOrWhiteSpace(x.MemberUserId) || x.Placement <= 0 || x.Points < 0))
		{
			return new IngestLeagueSeasonEventResultsError(IngestLeagueSeasonEventResultsErrorCode.InvalidRequest, "Result entries must include a member id, positive placement, and non-negative points.");
		}

		var memberIds = request.Request.Results
			.Select(x => x.MemberUserId.Trim())
			.ToArray();

		var existingResults = await context.LeagueSeasonEventResults
			.AsNoTracking()
			.Where(x => x.LeagueSeasonEventId == request.EventId)
			.Select(x => new
			{
				x.UserId,
				x.Placement,
				x.Points,
				x.ChipsDelta
			})
			.ToListAsync(cancellationToken);

		if (existingResults.Count > 0)
		{
			var existingNormalized = existingResults
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

			var isIdempotentReplay = existingNormalized.SequenceEqual(incomingNormalized);
			if (isIdempotentReplay)
			{
				return Unit.Value;
			}

			return new IngestLeagueSeasonEventResultsError(IngestLeagueSeasonEventResultsErrorCode.ResultsAlreadyIngested, "Results were already ingested for this event. Use correction workflow to change them.");
		}

		var activeMemberIds = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.IsActive && memberIds.Contains(x.UserId))
			.Select(x => x.UserId)
			.ToListAsync(cancellationToken);

		if (activeMemberIds.Count != memberIds.Length)
		{
			return new IngestLeagueSeasonEventResultsError(IngestLeagueSeasonEventResultsErrorCode.MemberNotFound, "All result entries must reference active league members.");
		}

		var now = DateTimeOffset.UtcNow;

		foreach (var result in request.Request.Results)
		{
			var trimmedMemberUserId = result.MemberUserId.Trim();
			context.LeagueSeasonEventResults.Add(new LeagueSeasonEventResult
			{
				LeagueId = request.LeagueId,
				LeagueSeasonId = request.SeasonId,
				LeagueSeasonEventId = request.EventId,
				UserId = trimmedMemberUserId,
				Placement = result.Placement,
				Points = result.Points,
				ChipsDelta = result.ChipsDelta,
				RecordedByUserId = currentUserService.UserId,
				RecordedAtUtc = now
			});

		}

		seasonEvent.Status = LeagueSeasonEventStatus.Completed;

		await context.SaveChangesAsync(cancellationToken);

		await LeagueSeasonEventStandingsRecalculator.RebuildForMembersAsync(context, request.LeagueId, memberIds, cancellationToken);
		await context.SaveChangesAsync(cancellationToken);

		return Unit.Value;
	}
}
