using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueInvites;

public sealed class GetLeagueInvitesQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetLeagueInvitesQuery, OneOf<IReadOnlyList<LeagueInviteSummaryDto>, GetLeagueInvitesError>>
{
	public async Task<OneOf<IReadOnlyList<LeagueInviteSummaryDto>, GetLeagueInvitesError>> Handle(GetLeagueInvitesQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetLeagueInvitesError(GetLeagueInvitesErrorCode.Unauthorized, "User is not authenticated.");
		}

		var isMember = await context.LeagueMembersCurrent
			.AnyAsync(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive, cancellationToken);

		if (!isMember)
		{
			return new GetLeagueInvitesError(GetLeagueInvitesErrorCode.Forbidden, "Only league members can view invites.");
		}

		var invites = await context.LeagueInvites
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId)
			.OrderByDescending(x => x.CreatedAtUtc)
			.Select(x => new LeagueInviteSummaryDto
			{
				InviteId = x.Id,
				LeagueId = x.LeagueId,
				Status = (Contracts.LeagueInviteStatus)x.Status,
				CreatedAtUtc = x.CreatedAtUtc,
				ExpiresAtUtc = x.ExpiresAtUtc,
				InviteCode = x.InviteCode
			})
			.ToListAsync(cancellationToken);

		return invites;
	}
}