using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;

public sealed class CreateLeagueCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<CreateLeagueCommand, OneOf<CreateLeagueResponse, CreateLeagueError>>
{
	public async Task<OneOf<CreateLeagueResponse, CreateLeagueError>> Handle(CreateLeagueCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new CreateLeagueError(CreateLeagueErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (string.IsNullOrWhiteSpace(request.Request.Name))
		{
			return new CreateLeagueError(CreateLeagueErrorCode.InvalidRequest, "League name is required.");
		}

		var now = DateTimeOffset.UtcNow;
		var trimmedName = request.Request.Name.Trim();
		var trimmedDescription = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim();

		var league = new League
		{
			Id = Guid.CreateVersion7(),
			Name = trimmedName,
			Description = trimmedDescription,
			CreatedByUserId = currentUserService.UserId,
			CreatedAtUtc = now,
			IsArchived = false
		};

		var creatorMembership = new LeagueMemberCurrent
		{
			LeagueId = league.Id,
			UserId = currentUserService.UserId,
			Role = CardGames.Poker.Api.Data.Entities.LeagueRole.Manager,
			IsActive = true,
			JoinedAtUtc = now,
			LeftAtUtc = null,
			UpdatedAtUtc = now
		};

		var joinedEvent = new LeagueMembershipEvent
		{
			Id = Guid.CreateVersion7(),
			LeagueId = league.Id,
			UserId = currentUserService.UserId,
			ActorUserId = currentUserService.UserId,
			EventType = LeagueMembershipEventType.MemberJoined,
			OccurredAtUtc = now
		};

		context.Leagues.Add(league);
		context.LeagueMembersCurrent.Add(creatorMembership);
		context.LeagueMembershipEvents.Add(joinedEvent);

		await context.SaveChangesAsync(cancellationToken);

		return new CreateLeagueResponse
		{
			LeagueId = league.Id,
			Name = league.Name,
			Description = league.Description,
			CreatedAtUtc = league.CreatedAtUtc,
			CreatedByUserId = league.CreatedByUserId,
			MyRole = (CardGames.Poker.Api.Contracts.LeagueRole)creatorMembership.Role
		};
	}
}