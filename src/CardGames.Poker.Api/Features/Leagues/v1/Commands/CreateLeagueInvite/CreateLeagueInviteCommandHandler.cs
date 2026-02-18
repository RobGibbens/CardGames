using System.Security.Cryptography;
using System.Text;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueInvite;

public sealed class CreateLeagueInviteCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<CreateLeagueInviteCommand, OneOf<CreateLeagueInviteResponse, CreateLeagueInviteError>>
{
	public async Task<OneOf<CreateLeagueInviteResponse, CreateLeagueInviteError>> Handle(CreateLeagueInviteCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new CreateLeagueInviteError(CreateLeagueInviteErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (request.Request.ExpiresAtUtc <= DateTimeOffset.UtcNow)
		{
			return new CreateLeagueInviteError(CreateLeagueInviteErrorCode.InvalidExpiry, "Invite expiry must be in the future.");
		}

		var league = await context.Leagues
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == request.LeagueId, cancellationToken);

		if (league is null)
		{
			return new CreateLeagueInviteError(CreateLeagueInviteErrorCode.LeagueNotFound, "League not found.");
		}

		var isAdmin = await context.LeagueMembersCurrent
			.AnyAsync(x => x.LeagueId == request.LeagueId &&
				x.UserId == currentUserService.UserId &&
				x.IsActive &&
				x.Role == Data.Entities.LeagueRole.Admin,
				cancellationToken);

		if (!isAdmin)
		{
			return new CreateLeagueInviteError(CreateLeagueInviteErrorCode.Forbidden, "Only league admins can create invites.");
		}

		var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
		var tokenHash = ComputeSha256(token);

		var invite = new LeagueInvite
		{
			Id = Guid.CreateVersion7(),
			LeagueId = request.LeagueId,
			TokenHash = tokenHash,
			Status = Data.Entities.LeagueInviteStatus.Active,
			CreatedByUserId = currentUserService.UserId,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			ExpiresAtUtc = request.Request.ExpiresAtUtc,
			RevokedAtUtc = null,
			RevokedByUserId = null
		};

		context.LeagueInvites.Add(invite);
		await context.SaveChangesAsync(cancellationToken);

		return new CreateLeagueInviteResponse
		{
			InviteId = invite.Id,
			LeagueId = invite.LeagueId,
			InviteUrl = $"/leagues/join/{token}",
			ExpiresAtUtc = invite.ExpiresAtUtc,
			Status = Contracts.LeagueInviteStatus.Active
		};
	}

	private static string ComputeSha256(string value)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		return Convert.ToHexString(bytes);
	}
}