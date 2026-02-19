using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Features.Leagues.v1.Queries;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueDetail;

public sealed class GetLeagueDetailQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetLeagueDetailQuery, OneOf<LeagueDetailDto, GetLeagueDetailError>>
{
	public async Task<OneOf<LeagueDetailDto, GetLeagueDetailError>> Handle(GetLeagueDetailQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetLeagueDetailError(GetLeagueDetailErrorCode.Unauthorized, "User is not authenticated.");
		}

		var currentUserId = currentUserService.UserId;

		var membership = await context.LeagueMembersCurrent
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.LeagueId == request.LeagueId && x.UserId == currentUserId && x.IsActive, cancellationToken);

		if (membership is null)
		{
			var isLeagueCreator = await context.Leagues
				.AsNoTracking()
				.AnyAsync(x => x.Id == request.LeagueId && x.CreatedByUserId == currentUserId, cancellationToken);

			if (!isLeagueCreator)
			{
				return new GetLeagueDetailError(GetLeagueDetailErrorCode.Forbidden, "Only active members can view league details.");
			}
		}

		var league = await context.Leagues
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == request.LeagueId, cancellationToken);

		if (league is null)
		{
			return new GetLeagueDetailError(GetLeagueDetailErrorCode.NotFound, "League not found.");
		}

		var activeMemberCount = await context.LeagueMembersCurrent
			.AsNoTracking()
			.CountAsync(x => x.LeagueId == request.LeagueId && x.IsActive, cancellationToken);

		var displayNamesByUserId = await LeagueUserDisplayNameResolver.GetDisplayNamesByUserIdAsync(
			context,
			[league.CreatedByUserId],
			cancellationToken);

		return new LeagueDetailDto
		{
			LeagueId = league.Id,
			Name = league.Name,
			Description = league.Description,
			CreatedAtUtc = league.CreatedAtUtc,
			CreatedByUserId = league.CreatedByUserId,
			CreatedByDisplayName = LeagueUserDisplayNameResolver.GetDisplayNameOrFallback(displayNamesByUserId, league.CreatedByUserId),
			MyRole = (Contracts.LeagueRole)LeagueGovernanceRules.ToCurrentUserProjectedRole(
				membership?.Role ?? Data.Entities.LeagueRole.Owner),
			ActiveMemberCount = activeMemberCount
		};
	}
}