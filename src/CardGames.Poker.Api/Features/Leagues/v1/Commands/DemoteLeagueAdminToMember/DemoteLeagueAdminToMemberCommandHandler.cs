using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.DemoteLeagueAdminToMember;

public sealed class DemoteLeagueAdminToMemberCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<DemoteLeagueAdminToMemberCommand, OneOf<Unit, DemoteLeagueAdminToMemberError>>
{
	public async Task<OneOf<Unit, DemoteLeagueAdminToMemberError>> Handle(DemoteLeagueAdminToMemberCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new DemoteLeagueAdminToMemberError(DemoteLeagueAdminToMemberErrorCode.Unauthorized, "User is not authenticated.");
		}

		var actorCanManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!actorCanManageLeague)
		{
			return new DemoteLeagueAdminToMemberError(DemoteLeagueAdminToMemberErrorCode.Forbidden, "Only league managers or admins can demote admins.");
		}

		var member = await context.LeagueMembersCurrent
			.FirstOrDefaultAsync(x => x.LeagueId == request.LeagueId && x.UserId == request.MemberUserId && x.IsActive, cancellationToken);

		if (member is null)
		{
			return new DemoteLeagueAdminToMemberError(DemoteLeagueAdminToMemberErrorCode.MemberNotFound, "Target member was not found in the league.");
		}

		if (member.Role != LeagueRole.Admin)
		{
			return new DemoteLeagueAdminToMemberError(DemoteLeagueAdminToMemberErrorCode.InvalidRequest, "Only active admins can be demoted with this operation.");
		}

		var activeRolesExcludingTarget = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.IsActive && x.UserId != request.MemberUserId)
			.Select(x => x.Role)
			.ToListAsync(cancellationToken);

		if (!activeRolesExcludingTarget.HasAtLeastOneGovernanceCapableMember())
		{
			return new DemoteLeagueAdminToMemberError(DemoteLeagueAdminToMemberErrorCode.Conflict, "League must retain at least one manager or admin.");
		}

		var now = DateTimeOffset.UtcNow;
		member.Role = LeagueRole.Member;
		member.UpdatedAtUtc = now;

		context.LeagueMembershipEvents.Add(new LeagueMembershipEvent
		{
			Id = Guid.CreateVersion7(),
			LeagueId = request.LeagueId,
			UserId = request.MemberUserId,
			ActorUserId = currentUserService.UserId,
			EventType = LeagueMembershipEventType.MemberDemotedFromAdmin,
			OccurredAtUtc = now
		});

		await context.SaveChangesAsync(cancellationToken);

		return Unit.Value;
	}
}
