using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.TransferLeagueOwnership;

public sealed class TransferLeagueOwnershipCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<TransferLeagueOwnershipCommand, OneOf<Unit, TransferLeagueOwnershipError>>
{
	public async Task<OneOf<Unit, TransferLeagueOwnershipError>> Handle(TransferLeagueOwnershipCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new TransferLeagueOwnershipError(TransferLeagueOwnershipErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (string.Equals(request.MemberUserId, currentUserService.UserId, StringComparison.Ordinal))
		{
			return new TransferLeagueOwnershipError(TransferLeagueOwnershipErrorCode.InvalidRequest, "Ownership must be transferred to another active member.");
		}

		var actorMembership = await context.LeagueMembersCurrent
			.FirstOrDefaultAsync(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive, cancellationToken);

		if (actorMembership is null || !LeagueGovernanceRules.IsManagerAuthority(actorMembership.Role))
		{
			return new TransferLeagueOwnershipError(TransferLeagueOwnershipErrorCode.Forbidden, "Only league owners or managers can transfer ownership.");
		}

		var targetMembership = await context.LeagueMembersCurrent
			.FirstOrDefaultAsync(x => x.LeagueId == request.LeagueId && x.UserId == request.MemberUserId && x.IsActive, cancellationToken);

		if (targetMembership is null)
		{
			return new TransferLeagueOwnershipError(TransferLeagueOwnershipErrorCode.MemberNotFound, "Target member was not found in the league.");
		}

		var now = DateTimeOffset.UtcNow;
		actorMembership.Role = LeagueRole.Manager;
		actorMembership.UpdatedAtUtc = now;

		targetMembership.Role = LeagueRole.Owner;
		targetMembership.UpdatedAtUtc = now;

		context.LeagueMembershipEvents.Add(new LeagueMembershipEvent
		{
			Id = Guid.CreateVersion7(),
			LeagueId = request.LeagueId,
			UserId = request.MemberUserId,
			ActorUserId = currentUserService.UserId,
			EventType = LeagueMembershipEventType.LeagueOwnershipTransferred,
			OccurredAtUtc = now
		});

		await context.SaveChangesAsync(cancellationToken);

		return Unit.Value;
	}
}
