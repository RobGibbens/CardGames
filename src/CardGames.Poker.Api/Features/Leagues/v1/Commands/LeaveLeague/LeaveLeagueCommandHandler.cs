using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.LeaveLeague;

public sealed class LeaveLeagueCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<LeaveLeagueCommand, OneOf<LeaveLeagueResponse, LeaveLeagueError>>
{
	public async Task<OneOf<LeaveLeagueResponse, LeaveLeagueError>> Handle(LeaveLeagueCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new LeaveLeagueError(LeaveLeagueErrorCode.Unauthorized, "User is not authenticated.");
		}

		var membership = await context.LeagueMembersCurrent
			.FirstOrDefaultAsync(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId, cancellationToken);

		if (membership is null || !membership.IsActive)
		{
			return new LeaveLeagueResponse
			{
				LeagueId = request.LeagueId,
				Left = false,
				WasActiveMember = false
			};
		}

		var activeMembers = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.IsActive)
			.Select(x => new { x.UserId, x.Role })
			.ToListAsync(cancellationToken);

		if (membership.Role == Data.Entities.LeagueRole.Manager)
		{
			var hasAnotherManager = activeMembers.Any(x => x.UserId != currentUserService.UserId && x.Role == Data.Entities.LeagueRole.Manager);
			if (!hasAnotherManager)
			{
				return new LeaveLeagueError(LeaveLeagueErrorCode.Conflict, "League must retain at least one manager.");
			}
		}

		if (LeagueGovernanceRules.IsGovernanceCapable(membership.Role))
		{
			var hasAnotherGovernanceMember = activeMembers.Any(x =>
				x.UserId != currentUserService.UserId &&
				LeagueGovernanceRules.IsGovernanceCapable(x.Role));

			if (!hasAnotherGovernanceMember)
			{
				return new LeaveLeagueError(LeaveLeagueErrorCode.Conflict, "League must retain at least one manager or admin.");
			}
		}

		var now = DateTimeOffset.UtcNow;
		membership.IsActive = false;
		membership.LeftAtUtc = now;
		membership.UpdatedAtUtc = now;

		context.LeagueMembershipEvents.Add(new LeagueMembershipEvent
		{
			Id = Guid.CreateVersion7(),
			LeagueId = request.LeagueId,
			UserId = currentUserService.UserId,
			ActorUserId = currentUserService.UserId,
			EventType = LeagueMembershipEventType.MemberLeft,
			OccurredAtUtc = now
		});

		await context.SaveChangesAsync(cancellationToken);

		return new LeaveLeagueResponse
		{
			LeagueId = request.LeagueId,
			Left = true,
			WasActiveMember = true
		};
	}
}