using System.Security.Cryptography;
using System.Text;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
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

		if (invite is null || invite.Status != Data.Entities.LeagueInviteStatus.Active || invite.ExpiresAtUtc <= now)
		{
			return new JoinLeagueError(JoinLeagueErrorCode.InvalidInvite, "Invite is invalid, revoked, or expired.");
		}

		var membership = await context.LeagueMembersCurrent
			.FirstOrDefaultAsync(x => x.LeagueId == invite.LeagueId && x.UserId == currentUserService.UserId, cancellationToken);

		if (membership is not null && membership.IsActive)
		{
			return new JoinLeagueResponse
			{
				LeagueId = invite.LeagueId,
				Joined = false,
				AlreadyMember = true
			};
		}

		if (membership is null)
		{
			membership = new LeagueMemberCurrent
			{
				LeagueId = invite.LeagueId,
				UserId = currentUserService.UserId,
				Role = Data.Entities.LeagueRole.Member,
				IsActive = true,
				JoinedAtUtc = now,
				LeftAtUtc = null,
				UpdatedAtUtc = now
			};

			context.LeagueMembersCurrent.Add(membership);
		}
		else
		{
			membership.IsActive = true;
			membership.LeftAtUtc = null;
			membership.UpdatedAtUtc = now;
		}

		context.LeagueMembershipEvents.Add(new LeagueMembershipEvent
		{
			Id = Guid.CreateVersion7(),
			LeagueId = invite.LeagueId,
			UserId = currentUserService.UserId,
			ActorUserId = currentUserService.UserId,
			EventType = LeagueMembershipEventType.MemberJoined,
			OccurredAtUtc = now
		});

		await context.SaveChangesAsync(cancellationToken);

		return new JoinLeagueResponse
		{
			LeagueId = invite.LeagueId,
			Joined = true,
			AlreadyMember = false
		};
	}

	private static string ComputeSha256(string value)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		return Convert.ToHexString(bytes);
	}
}