using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueOneOffEvent;

public sealed class CreateLeagueOneOffEventCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<CreateLeagueOneOffEventCommand, OneOf<CreateLeagueOneOffEventResponse, CreateLeagueOneOffEventError>>
{
	public async Task<OneOf<CreateLeagueOneOffEventResponse, CreateLeagueOneOffEventError>> Handle(CreateLeagueOneOffEventCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (string.IsNullOrWhiteSpace(request.Request.Name))
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.InvalidRequest, "One-off event name is required.");
		}

		if (request.Request.ScheduledAtUtc == default)
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.InvalidRequest, "Scheduled date/time is required.");
		}

		var leagueExists = await context.Leagues
			.AsNoTracking()
			.AnyAsync(x => x.Id == request.LeagueId, cancellationToken);

		if (!leagueExists)
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.LeagueNotFound, "League not found.");
		}

		var isAdmin = await context.LeagueMembersCurrent
			.AsNoTracking()
			.AnyAsync(x => x.LeagueId == request.LeagueId &&
				x.UserId == currentUserService.UserId &&
				x.IsActive &&
				x.Role == Data.Entities.LeagueRole.Admin,
				cancellationToken);

		if (!isAdmin)
		{
			return new CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode.Forbidden, "Only league admins can create one-off events.");
		}

		var oneOffEvent = new LeagueOneOffEvent
		{
			Id = Guid.CreateVersion7(),
			LeagueId = request.LeagueId,
			Name = request.Request.Name.Trim(),
			ScheduledAtUtc = request.Request.ScheduledAtUtc,
			EventType = (Data.Entities.LeagueOneOffEventType)request.Request.EventType,
			Status = Data.Entities.LeagueOneOffEventStatus.Planned,
			Notes = string.IsNullOrWhiteSpace(request.Request.Notes) ? null : request.Request.Notes.Trim(),
			CreatedByUserId = currentUserService.UserId,
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		context.LeagueOneOffEvents.Add(oneOffEvent);
		await context.SaveChangesAsync(cancellationToken);

		return new CreateLeagueOneOffEventResponse
		{
			EventId = oneOffEvent.Id,
			LeagueId = oneOffEvent.LeagueId,
			Name = oneOffEvent.Name,
			ScheduledAtUtc = oneOffEvent.ScheduledAtUtc,
			EventType = request.Request.EventType,
			Status = (Contracts.LeagueOneOffEventStatus)oneOffEvent.Status,
			Notes = oneOffEvent.Notes,
			CreatedByUserId = oneOffEvent.CreatedByUserId,
			CreatedAtUtc = oneOffEvent.CreatedAtUtc
		};
	}
}