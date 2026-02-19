using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.PromoteLeagueMemberToAdmin;

public sealed class PromoteLeagueMemberToAdminCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<PromoteLeagueMemberToAdminCommand, OneOf<Unit, PromoteLeagueMemberToAdminError>>
{
	public async Task<OneOf<Unit, PromoteLeagueMemberToAdminError>> Handle(PromoteLeagueMemberToAdminCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new PromoteLeagueMemberToAdminError(PromoteLeagueMemberToAdminErrorCode.Unauthorized, "User is not authenticated.");
		}

		var actorCanManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x =>
				x.LeagueId == request.LeagueId &&
				x.UserId == currentUserService.UserId &&
				x.IsActive &&
				x.Role == LeagueRole.Manager,
				cancellationToken);

		if (!actorCanManageLeague)
		{
			return new PromoteLeagueMemberToAdminError(PromoteLeagueMemberToAdminErrorCode.Forbidden, "Only league managers can promote members.");
		}

		var member = await context.LeagueMembersCurrent
			.FirstOrDefaultAsync(x => x.LeagueId == request.LeagueId && x.UserId == request.MemberUserId && x.IsActive, cancellationToken);

		if (member is null)
		{
			return new PromoteLeagueMemberToAdminError(PromoteLeagueMemberToAdminErrorCode.MemberNotFound, "Target member was not found in the league.");
		}

		if (member.Role == LeagueRole.Member)
		{
			member.Role = LeagueRole.Admin;
			member.UpdatedAtUtc = DateTimeOffset.UtcNow;

			context.LeagueMembershipEvents.Add(new LeagueMembershipEvent
			{
				Id = Guid.CreateVersion7(),
				LeagueId = request.LeagueId,
				UserId = request.MemberUserId,
				ActorUserId = currentUserService.UserId,
				EventType = LeagueMembershipEventType.MemberPromotedToAdmin,
				OccurredAtUtc = DateTimeOffset.UtcNow
			});

			await context.SaveChangesAsync(cancellationToken);
		}

		return Unit.Value;
	}
}