using System.Security.Cryptography;
using System.Text;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Features.Leagues.v1.Queries;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueJoinPreview;

public sealed class GetLeagueJoinPreviewQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetLeagueJoinPreviewQuery, OneOf<LeagueJoinPreviewDto, GetLeagueJoinPreviewError>>
{
	public async Task<OneOf<LeagueJoinPreviewDto, GetLeagueJoinPreviewError>> Handle(GetLeagueJoinPreviewQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetLeagueJoinPreviewError(GetLeagueJoinPreviewErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (string.IsNullOrWhiteSpace(request.Token))
		{
			return new GetLeagueJoinPreviewError(GetLeagueJoinPreviewErrorCode.InvalidInvite, "Invite code is required.");
		}

		var tokenHash = ComputeSha256(request.Token.Trim());
		var now = DateTimeOffset.UtcNow;

		var invite = await context.LeagueInvites
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

		if (invite is null || invite.Status != Data.Entities.LeagueInviteStatus.Active || invite.ExpiresAtUtc <= now)
		{
			return new GetLeagueJoinPreviewError(GetLeagueJoinPreviewErrorCode.InvalidInvite, "Invite is invalid, revoked, or expired.");
		}

		var league = await context.Leagues
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == invite.LeagueId, cancellationToken);

		if (league is null)
		{
			return new GetLeagueJoinPreviewError(GetLeagueJoinPreviewErrorCode.InvalidInvite, "Invite is invalid, revoked, or expired.");
		}

		var activeMemberCount = await context.LeagueMembersCurrent
			.AsNoTracking()
			.CountAsync(x => x.LeagueId == invite.LeagueId && x.IsActive, cancellationToken);

		var displayNamesByUserId = await LeagueUserDisplayNameResolver.GetDisplayNamesByUserIdAsync(
			context,
			[league.CreatedByUserId],
			cancellationToken);

		return new LeagueJoinPreviewDto
		{
			LeagueId = league.Id,
			LeagueName = league.Name,
			LeagueDescription = league.Description,
			ManagerDisplayName = LeagueUserDisplayNameResolver.GetDisplayNameOrFallback(displayNamesByUserId, league.CreatedByUserId),
			ActiveMemberCount = activeMemberCount,
			JoinPolicy = "Private league access by code or invite. Duplicate submissions are safe."
		};
	}

	private static string ComputeSha256(string value)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		return Convert.ToHexString(bytes);
	}
}