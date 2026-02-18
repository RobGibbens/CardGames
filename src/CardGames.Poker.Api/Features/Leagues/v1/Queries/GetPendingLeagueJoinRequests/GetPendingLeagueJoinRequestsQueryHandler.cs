using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Features.Leagues.v1.Queries;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetPendingLeagueJoinRequests;

public sealed class GetPendingLeagueJoinRequestsQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetPendingLeagueJoinRequestsQuery, OneOf<IReadOnlyList<LeagueJoinRequestQueueItemDto>, GetPendingLeagueJoinRequestsError>>
{
	public async Task<OneOf<IReadOnlyList<LeagueJoinRequestQueueItemDto>, GetPendingLeagueJoinRequestsError>> Handle(GetPendingLeagueJoinRequestsQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetPendingLeagueJoinRequestsError(GetPendingLeagueJoinRequestsErrorCode.Unauthorized, "User is not authenticated.");
		}

		var canManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!canManageLeague)
		{
			return new GetPendingLeagueJoinRequestsError(GetPendingLeagueJoinRequestsErrorCode.Forbidden, "Only league managers or admins can view pending join requests.");
		}

		var now = DateTimeOffset.UtcNow;
		var expiredPendingRequests = await context.LeagueJoinRequests
			.Where(x => x.LeagueId == request.LeagueId && x.Status == Data.Entities.LeagueJoinRequestStatus.Pending && x.ExpiresAtUtc <= now)
			.ToListAsync(cancellationToken);

		if (expiredPendingRequests.Count > 0)
		{
			foreach (var expiredRequest in expiredPendingRequests)
			{
				expiredRequest.Status = Data.Entities.LeagueJoinRequestStatus.Expired;
				expiredRequest.UpdatedAtUtc = now;
				expiredRequest.ResolvedAtUtc = now;
				expiredRequest.ResolvedByUserId = null;
				expiredRequest.ResolutionReason = "Join request expired before moderation.";
			}

			await context.SaveChangesAsync(cancellationToken);
		}

		var pendingRequests = await context.LeagueJoinRequests
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.Status == Data.Entities.LeagueJoinRequestStatus.Pending)
			.OrderBy(x => x.CreatedAtUtc)
			.Select(x => new
			{
				x.Id,
				x.LeagueId,
				x.InviteId,
				x.RequesterUserId,
				x.CreatedAtUtc,
				x.ExpiresAtUtc
			})
			.ToListAsync(cancellationToken);

		var displayNamesByUserId = await LeagueUserDisplayNameResolver.GetDisplayNamesByUserIdAsync(
			context,
			pendingRequests.Select(x => x.RequesterUserId),
			cancellationToken);

		var result = pendingRequests
			.Select(x => new LeagueJoinRequestQueueItemDto
			{
				JoinRequestId = x.Id,
				LeagueId = x.LeagueId,
				InviteId = x.InviteId,
				RequesterUserId = x.RequesterUserId,
				RequesterDisplayName = LeagueUserDisplayNameResolver.GetDisplayNameOrFallback(displayNamesByUserId, x.RequesterUserId),
				Status = Contracts.LeagueJoinRequestStatus.Pending,
				CreatedAtUtc = x.CreatedAtUtc,
				ExpiresAtUtc = x.ExpiresAtUtc
			})
			.ToList();

		return result;
	}
}
