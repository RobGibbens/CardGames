using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.DenyLeagueJoinRequest;

public sealed class DenyLeagueJoinRequestCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<DenyLeagueJoinRequestCommand, OneOf<Unit, DenyLeagueJoinRequestError>>
{
	public async Task<OneOf<Unit, DenyLeagueJoinRequestError>> Handle(DenyLeagueJoinRequestCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new DenyLeagueJoinRequestError(DenyLeagueJoinRequestErrorCode.Unauthorized, "User is not authenticated.");
		}

		var canManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!canManageLeague)
		{
			return new DenyLeagueJoinRequestError(DenyLeagueJoinRequestErrorCode.Forbidden, "Only league managers or admins can deny join requests.");
		}

		var joinRequest = await context.LeagueJoinRequests
			.FirstOrDefaultAsync(x => x.LeagueId == request.LeagueId && x.Id == request.JoinRequestId, cancellationToken);

		if (joinRequest is null)
		{
			return new DenyLeagueJoinRequestError(DenyLeagueJoinRequestErrorCode.JoinRequestNotFound, "Join request not found.");
		}

		var now = DateTimeOffset.UtcNow;
		if (joinRequest.Status == LeagueJoinRequestStatus.Pending && joinRequest.ExpiresAtUtc <= now)
		{
			joinRequest.Status = LeagueJoinRequestStatus.Expired;
			joinRequest.UpdatedAtUtc = now;
			joinRequest.ResolvedAtUtc = now;
			joinRequest.ResolvedByUserId = null;
			joinRequest.ResolutionReason = "Join request expired before moderation.";
			await context.SaveChangesAsync(cancellationToken);
			return new DenyLeagueJoinRequestError(DenyLeagueJoinRequestErrorCode.InvalidState, "Join request has expired.");
		}

		if (joinRequest.Status == LeagueJoinRequestStatus.Approved)
		{
			return new DenyLeagueJoinRequestError(DenyLeagueJoinRequestErrorCode.InvalidState, "Join request has already been approved.");
		}

		if (joinRequest.Status == LeagueJoinRequestStatus.Expired)
		{
			return new DenyLeagueJoinRequestError(DenyLeagueJoinRequestErrorCode.InvalidState, "Join request has expired.");
		}

		if (joinRequest.Status == LeagueJoinRequestStatus.Denied)
		{
			return Unit.Value;
		}

		joinRequest.Status = LeagueJoinRequestStatus.Denied;
		joinRequest.UpdatedAtUtc = now;
		joinRequest.ResolvedAtUtc = now;
		joinRequest.ResolvedByUserId = currentUserService.UserId;
		joinRequest.ResolutionReason = request.Request.Reason?.Trim();

		await context.SaveChangesAsync(cancellationToken);
		return Unit.Value;
	}
}
