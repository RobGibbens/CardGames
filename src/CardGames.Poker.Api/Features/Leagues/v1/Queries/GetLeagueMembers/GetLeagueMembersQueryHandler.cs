using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueMembers;

public sealed class GetLeagueMembersQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetLeagueMembersQuery, OneOf<IReadOnlyList<LeagueMemberDto>, GetLeagueMembersError>>
{
	public async Task<OneOf<IReadOnlyList<LeagueMemberDto>, GetLeagueMembersError>> Handle(GetLeagueMembersQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetLeagueMembersError(GetLeagueMembersErrorCode.Unauthorized, "User is not authenticated.");
		}

		var isActiveMember = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive, cancellationToken);

		if (!isActiveMember)
		{
			return new GetLeagueMembersError(GetLeagueMembersErrorCode.Forbidden, "Only active members can view league members.");
		}

		var members = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId)
			.OrderByDescending(x => x.IsActive)
			.ThenBy(x => x.UserId)
			.Select(x => new LeagueMemberDto
			{
				UserId = x.UserId,
				Role = (Contracts.LeagueRole)x.Role,
				IsActive = x.IsActive,
				JoinedAtUtc = x.JoinedAtUtc,
				LeftAtUtc = x.LeftAtUtc
			})
			.ToListAsync(cancellationToken);

		return members;
	}
}