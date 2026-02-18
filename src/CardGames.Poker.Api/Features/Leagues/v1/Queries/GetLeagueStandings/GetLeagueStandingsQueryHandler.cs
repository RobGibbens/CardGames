using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueStandings;

public sealed class GetLeagueStandingsQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetLeagueStandingsQuery, OneOf<IReadOnlyList<LeagueStandingEntryDto>, GetLeagueStandingsError>>
{
	public async Task<OneOf<IReadOnlyList<LeagueStandingEntryDto>, GetLeagueStandingsError>> Handle(GetLeagueStandingsQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetLeagueStandingsError(GetLeagueStandingsErrorCode.Unauthorized, "User is not authenticated.");
		}

		var isActiveMember = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive, cancellationToken);

		if (!isActiveMember)
		{
			return new GetLeagueStandingsError(GetLeagueStandingsErrorCode.Forbidden, "Only active members can view standings.");
		}

		var standingRows = await context.LeagueStandingsCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId)
			.OrderByDescending(x => x.TotalPoints)
			.ThenByDescending(x => x.TotalChipsDelta)
			.ThenBy(x => x.UpdatedAtUtc)
			.ToListAsync(cancellationToken);

		var userIds = standingRows.Select(x => x.UserId).ToArray();
		var displayNamesByUserId = await Queries.LeagueUserDisplayNameResolver.GetDisplayNamesByUserIdAsync(context, userIds, cancellationToken);

		var results = new List<LeagueStandingEntryDto>(standingRows.Count);
		var rank = 0;
		var previousPoints = int.MinValue;
		var previousChipsDelta = int.MinValue;

		for (var index = 0; index < standingRows.Count; index++)
		{
			var row = standingRows[index];
			if (index == 0 || row.TotalPoints != previousPoints || row.TotalChipsDelta != previousChipsDelta)
			{
				rank = index + 1;
				previousPoints = row.TotalPoints;
				previousChipsDelta = row.TotalChipsDelta;
			}

			results.Add(new LeagueStandingEntryDto
			{
				UserId = row.UserId,
				UserDisplayName = Queries.LeagueUserDisplayNameResolver.GetDisplayNameOrFallback(displayNamesByUserId, row.UserId),
				Rank = rank,
				TotalEvents = row.TotalEvents,
				TotalPoints = row.TotalPoints,
				TotalChipsDelta = row.TotalChipsDelta,
				LastPlacement = row.LastPlacement,
				UpdatedAtUtc = row.UpdatedAtUtc
			});
		}

		return results;
	}
}
