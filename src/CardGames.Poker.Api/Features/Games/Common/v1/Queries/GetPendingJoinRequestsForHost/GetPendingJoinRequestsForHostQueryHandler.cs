using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetPendingJoinRequestsForHost;

public sealed class GetPendingJoinRequestsForHostQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetPendingJoinRequestsForHostQuery, IReadOnlyList<GameJoinRequestReceivedDto>>
{
	public async Task<IReadOnlyList<GameJoinRequestReceivedDto>> Handle(GetPendingJoinRequestsForHostQuery request, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return [];
		}

		var now = DateTimeOffset.UtcNow;

		var expiredRequests = await context.GameJoinRequests
			.Where(x => x.Status == GameJoinRequestStatus.Pending
				&& x.ExpiresAt <= now
				&& x.Game.CreatedById == currentUserService.UserId)
			.ToListAsync(cancellationToken);

		if (expiredRequests.Count > 0)
		{
			foreach (var expiredRequest in expiredRequests)
			{
				expiredRequest.Status = GameJoinRequestStatus.Expired;
				expiredRequest.UpdatedAt = now;
				expiredRequest.ResolvedAt = now;
				expiredRequest.ResolvedByUserId = currentUserService.UserId;
				expiredRequest.ResolvedByName = currentUserService.UserName;
				expiredRequest.ResolutionReason = "This join request expired before the host responded.";
			}

			await context.SaveChangesAsync(cancellationToken);
		}

		return await context.GameJoinRequests
			.AsNoTracking()
			.Where(x => x.Status == GameJoinRequestStatus.Pending
				&& x.ExpiresAt > now
				&& x.Game.CreatedById == currentUserService.UserId)
			.OrderBy(x => x.RequestedAt)
			.Select(x => new GameJoinRequestReceivedDto
			{
				GameId = x.GameId,
				JoinRequestId = x.Id,
				GameName = x.Game.Name ?? x.Game.GameType!.Name!,
				HostName = x.Game.CreatedByName ?? "Host",
				PlayerName = x.Player.Name,
				PlayerAvatarUrl = x.Player.AvatarUrl,
				PlayerFirstName = null,
				RequestedBuyIn = x.RequestedBuyIn,
				MaxBuyIn = x.Game.MaxBuyIn,
				RequestedAtUtc = x.RequestedAt,
				ExpiresAtUtc = x.ExpiresAt
			})
			.ToListAsync(cancellationToken);
	}
}