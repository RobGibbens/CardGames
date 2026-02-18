using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.RemoveLeagueMember;

public sealed class RemoveLeagueMemberCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<RemoveLeagueMemberCommand, OneOf<Unit, RemoveLeagueMemberError>>
{
	public async Task<OneOf<Unit, RemoveLeagueMemberError>> Handle(RemoveLeagueMemberCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new RemoveLeagueMemberError(RemoveLeagueMemberErrorCode.Unauthorized, "User is not authenticated.");
		}

		var actorCanManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!actorCanManageLeague)
		{
			return new RemoveLeagueMemberError(RemoveLeagueMemberErrorCode.Forbidden, "Only league managers or admins can remove members.");
		}

		if (string.Equals(request.MemberUserId, currentUserService.UserId, StringComparison.Ordinal))
		{
			return new RemoveLeagueMemberError(RemoveLeagueMemberErrorCode.InvalidRequest, "Use leave league for self-removal.");
		}

		var member = await context.LeagueMembersCurrent
			.FirstOrDefaultAsync(x => x.LeagueId == request.LeagueId && x.UserId == request.MemberUserId && x.IsActive, cancellationToken);

		if (member is null)
		{
			return new RemoveLeagueMemberError(RemoveLeagueMemberErrorCode.MemberNotFound, "Target member was not found in the league.");
		}

		var activeRolesExcludingTarget = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.IsActive && x.UserId != request.MemberUserId)
			.Select(x => x.Role)
			.ToListAsync(cancellationToken);

		if (member.Role == LeagueRole.Manager && !activeRolesExcludingTarget.HasAtLeastOneManager())
		{
			return new RemoveLeagueMemberError(RemoveLeagueMemberErrorCode.Conflict, "League must retain at least one manager.");
		}

		if (LeagueGovernanceRules.IsGovernanceCapable(member.Role) && !activeRolesExcludingTarget.HasAtLeastOneGovernanceCapableMember())
		{
			return new RemoveLeagueMemberError(RemoveLeagueMemberErrorCode.Conflict, "League must retain at least one manager or admin.");
		}

		var now = DateTimeOffset.UtcNow;
		member.IsActive = false;
		member.LeftAtUtc = now;
		member.UpdatedAtUtc = now;

		context.LeagueMembershipEvents.Add(new LeagueMembershipEvent
		{
			Id = Guid.CreateVersion7(),
			LeagueId = request.LeagueId,
			UserId = request.MemberUserId,
			ActorUserId = currentUserService.UserId,
			EventType = LeagueMembershipEventType.MemberLeft,
			OccurredAtUtc = now
		});

		await context.SaveChangesAsync(cancellationToken);

		return Unit.Value;
	}
}
