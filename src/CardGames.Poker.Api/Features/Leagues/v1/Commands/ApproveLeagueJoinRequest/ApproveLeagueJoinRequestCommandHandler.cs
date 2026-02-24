using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using CardGames.Contracts.SignalR;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.ApproveLeagueJoinRequest;

public sealed class ApproveLeagueJoinRequestCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	ILeagueBroadcaster leagueBroadcaster,
	ILogger<ApproveLeagueJoinRequestCommandHandler> logger)
	: IRequestHandler<ApproveLeagueJoinRequestCommand, OneOf<Unit, ApproveLeagueJoinRequestError>>
{
	public async Task<OneOf<Unit, ApproveLeagueJoinRequestError>> Handle(ApproveLeagueJoinRequestCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new ApproveLeagueJoinRequestError(ApproveLeagueJoinRequestErrorCode.Unauthorized, "User is not authenticated.");
		}

		var canManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!canManageLeague)
		{
			return new ApproveLeagueJoinRequestError(ApproveLeagueJoinRequestErrorCode.Forbidden, "Only league managers or admins can approve join requests.");
		}

		var joinRequest = await context.LeagueJoinRequests
			.FirstOrDefaultAsync(x => x.LeagueId == request.LeagueId && x.Id == request.JoinRequestId, cancellationToken);

		if (joinRequest is null)
		{
			return new ApproveLeagueJoinRequestError(ApproveLeagueJoinRequestErrorCode.JoinRequestNotFound, "Join request not found.");
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

			try
			{
				await leagueBroadcaster.BroadcastJoinRequestUpdatedAsync(new LeagueJoinRequestUpdatedDto
				{
					LeagueId = request.LeagueId,
					JoinRequestId = joinRequest.Id,
					Status = LeagueJoinRequestStatus.Expired.ToString(),
					UpdatedAtUtc = joinRequest.UpdatedAtUtc
				}, cancellationToken);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex,
					"Failed to broadcast join request expired update for league {LeagueId}, request {JoinRequestId}",
					request.LeagueId,
					joinRequest.Id);
			}

			return new ApproveLeagueJoinRequestError(ApproveLeagueJoinRequestErrorCode.InvalidState, "Join request has expired.");
		}

		if (joinRequest.Status == LeagueJoinRequestStatus.Denied)
		{
			return new ApproveLeagueJoinRequestError(ApproveLeagueJoinRequestErrorCode.InvalidState, "Join request has already been denied.");
		}

		if (joinRequest.Status == LeagueJoinRequestStatus.Expired)
		{
			return new ApproveLeagueJoinRequestError(ApproveLeagueJoinRequestErrorCode.InvalidState, "Join request has expired.");
		}

		if (joinRequest.Status == LeagueJoinRequestStatus.Approved)
		{
			return Unit.Value;
		}

		var membership = await context.LeagueMembersCurrent
			.FirstOrDefaultAsync(x => x.LeagueId == request.LeagueId && x.UserId == joinRequest.RequesterUserId, cancellationToken);

		var createdOrActivatedMembership = false;
		if (membership is null)
		{
			membership = new LeagueMemberCurrent
			{
				LeagueId = request.LeagueId,
				UserId = joinRequest.RequesterUserId,
				Role = LeagueRole.Member,
				IsActive = true,
				JoinedAtUtc = now,
				LeftAtUtc = null,
				UpdatedAtUtc = now
			};

			context.LeagueMembersCurrent.Add(membership);
			createdOrActivatedMembership = true;
		}
		else if (!membership.IsActive)
		{
			membership.IsActive = true;
			membership.LeftAtUtc = null;
			membership.UpdatedAtUtc = now;
			createdOrActivatedMembership = true;
		}

		if (createdOrActivatedMembership)
		{
			context.LeagueMembershipEvents.Add(new LeagueMembershipEvent
			{
				Id = Guid.CreateVersion7(),
				LeagueId = request.LeagueId,
				UserId = joinRequest.RequesterUserId,
				ActorUserId = currentUserService.UserId,
				EventType = LeagueMembershipEventType.MemberJoined,
				OccurredAtUtc = now
			});
		}

		joinRequest.Status = LeagueJoinRequestStatus.Approved;
		joinRequest.UpdatedAtUtc = now;
		joinRequest.ResolvedAtUtc = now;
		joinRequest.ResolvedByUserId = currentUserService.UserId;
		joinRequest.ResolutionReason = request.Request.Reason?.Trim();

		await context.SaveChangesAsync(cancellationToken);

		try
		{
			await leagueBroadcaster.BroadcastJoinRequestUpdatedAsync(new LeagueJoinRequestUpdatedDto
			{
				LeagueId = request.LeagueId,
				JoinRequestId = joinRequest.Id,
				Status = LeagueJoinRequestStatus.Approved.ToString(),
				UpdatedAtUtc = joinRequest.UpdatedAtUtc
			}, cancellationToken);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex,
				"Failed to broadcast join request approved update for league {LeagueId}, request {JoinRequestId}",
				request.LeagueId,
				joinRequest.Id);
		}

		return Unit.Value;
	}
}
