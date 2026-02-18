using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.RevokeLeagueInvite;

public sealed class RevokeLeagueInviteCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<RevokeLeagueInviteCommand, OneOf<Unit, RevokeLeagueInviteError>>
{
	public async Task<OneOf<Unit, RevokeLeagueInviteError>> Handle(RevokeLeagueInviteCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new RevokeLeagueInviteError(RevokeLeagueInviteErrorCode.Unauthorized, "User is not authenticated.");
		}

		var isAdmin = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == request.LeagueId &&
				x.UserId == currentUserService.UserId &&
				x.IsActive &&
				x.Role == Data.Entities.LeagueRole.Admin,
				cancellationToken);

		if (!isAdmin)
		{
			return new RevokeLeagueInviteError(RevokeLeagueInviteErrorCode.Forbidden, "Only league admins can revoke invites.");
		}

		var invite = await context.LeagueInvites
			.FirstOrDefaultAsync(x => x.Id == request.InviteId && x.LeagueId == request.LeagueId, cancellationToken);

		if (invite is null)
		{
			return new RevokeLeagueInviteError(RevokeLeagueInviteErrorCode.InviteNotFound, "Invite not found.");
		}

		if (invite.Status != Data.Entities.LeagueInviteStatus.Revoked)
		{
			invite.Status = Data.Entities.LeagueInviteStatus.Revoked;
			invite.RevokedAtUtc = DateTimeOffset.UtcNow;
			invite.RevokedByUserId = currentUserService.UserId;
			await context.SaveChangesAsync(cancellationToken);
		}

		return Unit.Value;
	}
}