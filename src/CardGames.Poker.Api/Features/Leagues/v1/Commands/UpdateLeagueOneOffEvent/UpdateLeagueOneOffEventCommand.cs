using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.UpdateLeagueOneOffEvent;

public sealed record UpdateLeagueOneOffEventCommand(Guid LeagueId, Guid EventId, UpdateLeagueOneOffEventRequest Request)
	: IRequest<OneOf<Unit, UpdateLeagueOneOffEventError>>;

public enum UpdateLeagueOneOffEventErrorCode
{
	Unauthorized,
	Forbidden,
	LeagueNotFound,
	EventNotFound,
	InvalidRequest,
	Conflict
}

public sealed record UpdateLeagueOneOffEventError(UpdateLeagueOneOffEventErrorCode Code, string Message);