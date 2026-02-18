using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueMembershipHistory;

public sealed class GetLeagueMembershipHistoryQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetLeagueMembershipHistoryQuery, OneOf<IReadOnlyList<LeagueMembershipHistoryItemDto>, GetLeagueMembershipHistoryError>>
{
	public async Task<OneOf<IReadOnlyList<LeagueMembershipHistoryItemDto>, GetLeagueMembershipHistoryError>> Handle(GetLeagueMembershipHistoryQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetLeagueMembershipHistoryError(GetLeagueMembershipHistoryErrorCode.Unauthorized, "User is not authenticated.");
		}

		var isActiveMember = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive, cancellationToken);

		if (!isActiveMember)
		{
			return new GetLeagueMembershipHistoryError(GetLeagueMembershipHistoryErrorCode.Forbidden, "Only active members can view membership history.");
		}

		var history = await context.LeagueMembershipEvents
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId)
			.OrderByDescending(x => x.OccurredAtUtc)
			.ThenByDescending(x => x.Id)
			.Select(x => new LeagueMembershipHistoryItemDto
			{
				EventId = x.Id,
				LeagueId = x.LeagueId,
				UserId = x.UserId,
				ActorUserId = x.ActorUserId,
				EventType = (Contracts.LeagueMembershipHistoryEventType)x.EventType,
				OccurredAtUtc = x.OccurredAtUtc
			})
			.ToListAsync(cancellationToken);

		return history;
	}
}
