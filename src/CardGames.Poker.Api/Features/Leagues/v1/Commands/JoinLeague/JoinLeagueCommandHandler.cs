using System.Security.Cryptography;
using System.Text;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.JoinLeague;

public sealed class JoinLeagueCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<JoinLeagueCommand, OneOf<JoinLeagueResponse, JoinLeagueError>>
{
	public async Task<OneOf<JoinLeagueResponse, JoinLeagueError>> Handle(JoinLeagueCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new JoinLeagueError(JoinLeagueErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (string.IsNullOrWhiteSpace(request.Request.Token))
		{
			return new JoinLeagueError(JoinLeagueErrorCode.InvalidInvite, "Invite token is required.");
		}

		var tokenHash = ComputeSha256(request.Request.Token.Trim());
		var now = DateTimeOffset.UtcNow;

		var invite = await context.LeagueInvites
			.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

		if (invite is null)
		{
			return new JoinLeagueError(JoinLeagueErrorCode.InvalidInvite, "Invite token is invalid.");
		}

		if (invite.Status == Data.Entities.LeagueInviteStatus.Revoked)
		{
			return new JoinLeagueError(JoinLeagueErrorCode.InviteRevoked, "Invite has been revoked.");
		}

		if (invite.ExpiresAtUtc <= now)
		{
			return new JoinLeagueError(JoinLeagueErrorCode.InviteExpired, "Invite has expired.");
		}

		if (invite.Status != Data.Entities.LeagueInviteStatus.Active)
		{
			return new JoinLeagueError(JoinLeagueErrorCode.InvalidInvite, "Invite token is invalid.");
		}

		var membership = await context.LeagueMembersCurrent
			.FirstOrDefaultAsync(x => x.LeagueId == invite.LeagueId && x.UserId == currentUserService.UserId, cancellationToken);

		if (membership is not null && membership.IsActive)
		{
			return new JoinLeagueResponse
			{
				LeagueId = invite.LeagueId,
				JoinRequestId = null,
				JoinRequestStatus = Contracts.LeagueJoinRequestStatus.Approved,
				RequestSubmitted = false,
				Joined = false,
				AlreadyMember = true
			};
		}

		var existingPendingRequest = await context.LeagueJoinRequests
			.FirstOrDefaultAsync(x =>
				x.LeagueId == invite.LeagueId &&
				x.RequesterUserId == currentUserService.UserId &&
				x.Status == Data.Entities.LeagueJoinRequestStatus.Pending,
				cancellationToken);

		if (existingPendingRequest is not null && existingPendingRequest.ExpiresAtUtc <= now)
		{
			existingPendingRequest.Status = Data.Entities.LeagueJoinRequestStatus.Expired;
			existingPendingRequest.UpdatedAtUtc = now;
			existingPendingRequest.ResolvedAtUtc = now;
			existingPendingRequest.ResolvedByUserId = null;
			existingPendingRequest.ResolutionReason = "Join request expired before moderation.";
			existingPendingRequest = null;
		}

		if (existingPendingRequest is null)
		{
			existingPendingRequest = new LeagueJoinRequest
			{
				Id = Guid.CreateVersion7(),
				LeagueId = invite.LeagueId,
				InviteId = invite.Id,
				RequesterUserId = currentUserService.UserId,
				Status = Data.Entities.LeagueJoinRequestStatus.Pending,
				CreatedAtUtc = now,
				UpdatedAtUtc = now,
				ExpiresAtUtc = invite.ExpiresAtUtc,
				ResolvedAtUtc = null,
				ResolvedByUserId = null,
				ResolutionReason = null
			};

			context.LeagueJoinRequests.Add(existingPendingRequest);
		}
		else
		{
			existingPendingRequest.InviteId = invite.Id;
			existingPendingRequest.ExpiresAtUtc = invite.ExpiresAtUtc;
			existingPendingRequest.UpdatedAtUtc = now;
		}

		try
		{
			await context.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlException && (sqlException.Number == 2601 || sqlException.Number == 2627))
		{
			existingPendingRequest = await context.LeagueJoinRequests
				.AsNoTracking()
				.FirstOrDefaultAsync(x =>
					x.LeagueId == invite.LeagueId &&
					x.RequesterUserId == currentUserService.UserId &&
					x.Status == Data.Entities.LeagueJoinRequestStatus.Pending,
					cancellationToken);

			if (existingPendingRequest is null)
			{
				throw;
			}
		}

		return new JoinLeagueResponse
		{
			LeagueId = invite.LeagueId,
			JoinRequestId = existingPendingRequest.Id,
			JoinRequestStatus = Contracts.LeagueJoinRequestStatus.Pending,
			RequestSubmitted = true,
			Joined = false,
			AlreadyMember = false
		};
	}

	private static string ComputeSha256(string value)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		return Convert.ToHexString(bytes);
	}
}