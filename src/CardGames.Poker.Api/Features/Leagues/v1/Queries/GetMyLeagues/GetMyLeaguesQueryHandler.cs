using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetMyLeagues;

public sealed class GetMyLeaguesQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetMyLeaguesQuery, OneOf<IReadOnlyList<LeagueSummaryDto>, GetMyLeaguesError>>
{
	public async Task<OneOf<IReadOnlyList<LeagueSummaryDto>, GetMyLeaguesError>> Handle(GetMyLeaguesQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new GetMyLeaguesError(GetMyLeaguesErrorCode.Unauthorized, "User is not authenticated.");
		}

		var leagues = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.UserId == currentUserService.UserId && x.IsActive)
			.Select(x => new LeagueSummaryDto
			{
				LeagueId = x.LeagueId,
				Name = x.League.Name,
				Description = x.League.Description,
				Role = (CardGames.Poker.Api.Contracts.LeagueRole)LeagueGovernanceRules.ToCurrentUserProjectedRole(x.Role),
				CreatedAtUtc = x.League.CreatedAtUtc
			})
			.OrderByDescending(x => x.CreatedAtUtc)
			.ToListAsync(cancellationToken);

		return leagues;
	}
}