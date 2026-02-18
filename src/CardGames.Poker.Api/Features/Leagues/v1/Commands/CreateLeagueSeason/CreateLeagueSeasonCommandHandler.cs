using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Leagues.v1.Governance;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeason;

public sealed class CreateLeagueSeasonCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<CreateLeagueSeasonCommand, OneOf<CreateLeagueSeasonResponse, CreateLeagueSeasonError>>
{
	public async Task<OneOf<CreateLeagueSeasonResponse, CreateLeagueSeasonError>> Handle(CreateLeagueSeasonCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new CreateLeagueSeasonError(CreateLeagueSeasonErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (string.IsNullOrWhiteSpace(request.Request.Name))
		{
			return new CreateLeagueSeasonError(CreateLeagueSeasonErrorCode.InvalidRequest, "Season name is required.");
		}

		if (request.Request.PlannedEventCount.HasValue && request.Request.PlannedEventCount.Value <= 0)
		{
			return new CreateLeagueSeasonError(CreateLeagueSeasonErrorCode.InvalidRequest, "Planned event count must be greater than zero.");
		}

		if (request.Request.StartsAtUtc.HasValue && request.Request.EndsAtUtc.HasValue && request.Request.StartsAtUtc > request.Request.EndsAtUtc)
		{
			return new CreateLeagueSeasonError(CreateLeagueSeasonErrorCode.InvalidRequest, "Season start must be before season end.");
		}

		var leagueExists = await context.Leagues
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.LeagueId, cancellationToken);

		if (!leagueExists)
		{
			return new CreateLeagueSeasonError(CreateLeagueSeasonErrorCode.LeagueNotFound, "League not found.");
		}

		var canManageLeague = await context.LeagueMembersCurrent
			.AsNoTracking()
			.Where(x => x.LeagueId == request.LeagueId && x.UserId == currentUserService.UserId && x.IsActive)
			.GovernanceCapableMembers()
			.AnyAsync(cancellationToken);

		if (!canManageLeague)
		{
			return new CreateLeagueSeasonError(CreateLeagueSeasonErrorCode.Forbidden, "Only league managers or admins can create seasons.");
		}

		var season = new LeagueSeason
		{
			Id = Guid.CreateVersion7(),
			LeagueId = request.LeagueId,
			Name = request.Request.Name.Trim(),
			PlannedEventCount = request.Request.PlannedEventCount,
			StartsAtUtc = request.Request.StartsAtUtc,
			EndsAtUtc = request.Request.EndsAtUtc,
			Status = Data.Entities.LeagueSeasonStatus.Planned,
			CreatedByUserId = currentUserService.UserId,
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		context.LeagueSeasons.Add(season);
		await context.SaveChangesAsync(cancellationToken);

		return new CreateLeagueSeasonResponse
		{
			SeasonId = season.Id,
			LeagueId = season.LeagueId,
			Name = season.Name,
			PlannedEventCount = season.PlannedEventCount,
			StartsAtUtc = season.StartsAtUtc,
			EndsAtUtc = season.EndsAtUtc,
			Status = (Contracts.LeagueSeasonStatus)season.Status,
			CreatedByUserId = season.CreatedByUserId,
			CreatedAtUtc = season.CreatedAtUtc
		};
	}
}