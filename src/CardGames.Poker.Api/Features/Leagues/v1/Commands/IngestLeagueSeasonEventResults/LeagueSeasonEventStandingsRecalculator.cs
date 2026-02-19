using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;

internal static class LeagueSeasonEventStandingsRecalculator
{
	public static async Task RebuildForMembersAsync(
		CardsDbContext context,
		Guid leagueId,
		IReadOnlyCollection<string> memberUserIds,
		CancellationToken cancellationToken)
	{
		var normalizedMemberIds = memberUserIds
			.Where(x => !string.IsNullOrWhiteSpace(x))
			.Select(x => x.Trim())
			.Distinct(StringComparer.Ordinal)
			.ToArray();

		if (normalizedMemberIds.Length == 0)
		{
			return;
		}

		var aggregates = await context.LeagueSeasonEventResults
			.AsNoTracking()
			.Where(x => x.LeagueId == leagueId && normalizedMemberIds.Contains(x.UserId))
			.GroupBy(x => x.UserId)
			.Select(group => new
			{
				UserId = group.Key,
				TotalEvents = group.Count(),
				TotalPoints = group.Sum(x => x.Points),
				TotalChipsDelta = group.Sum(x => x.ChipsDelta)
			})
			.ToDictionaryAsync(x => x.UserId, StringComparer.Ordinal, cancellationToken);

		var latestResultByUserId = await context.LeagueSeasonEventResults
			.AsNoTracking()
			.Where(x => x.LeagueId == leagueId && normalizedMemberIds.Contains(x.UserId))
			.GroupBy(x => x.UserId)
			.Select(group => group
				.OrderByDescending(x => x.RecordedAtUtc)
				.ThenBy(x => x.Placement)
				.Select(x => new
				{
					x.UserId,
					x.Placement,
					x.RecordedAtUtc
				})
				.First())
			.ToDictionaryAsync(x => x.UserId, StringComparer.Ordinal, cancellationToken);

		var standingsByUserId = await context.LeagueStandingsCurrent
			.Where(x => x.LeagueId == leagueId && normalizedMemberIds.Contains(x.UserId))
			.ToDictionaryAsync(x => x.UserId, StringComparer.Ordinal, cancellationToken);

		var now = DateTimeOffset.UtcNow;
		foreach (var memberUserId in normalizedMemberIds)
		{
			if (!standingsByUserId.TryGetValue(memberUserId, out var standing))
			{
				standing = new LeagueStandingCurrent
				{
					LeagueId = leagueId,
					UserId = memberUserId,
					UpdatedAtUtc = now
				};
				context.LeagueStandingsCurrent.Add(standing);
				standingsByUserId[memberUserId] = standing;
			}

			if (aggregates.TryGetValue(memberUserId, out var aggregate))
			{
				standing.TotalEvents = aggregate.TotalEvents;
				standing.TotalPoints = aggregate.TotalPoints;
				standing.TotalChipsDelta = aggregate.TotalChipsDelta;

				if (latestResultByUserId.TryGetValue(memberUserId, out var latest))
				{
					standing.LastPlacement = latest.Placement;
					standing.LastEventRecordedAtUtc = latest.RecordedAtUtc;
				}
				else
				{
					standing.LastPlacement = null;
					standing.LastEventRecordedAtUtc = null;
				}
			}
			else
			{
				standing.TotalEvents = 0;
				standing.TotalPoints = 0;
				standing.TotalChipsDelta = 0;
				standing.LastPlacement = null;
				standing.LastEventRecordedAtUtc = null;
			}

			standing.UpdatedAtUtc = now;
		}
	}
}
