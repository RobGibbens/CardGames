using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.UpdateLeagueSeasonEvent;

public sealed record UpdateLeagueSeasonEventCommand(Guid LeagueId, Guid SeasonId, Guid EventId, UpdateLeagueSeasonEventRequest Request)
	: IRequest<OneOf<Unit, UpdateLeagueSeasonEventError>>;

public enum UpdateLeagueSeasonEventErrorCode
{
	Unauthorized,
	Forbidden,
	LeagueNotFound,
	SeasonNotFound,
	EventNotFound,
	InvalidRequest,
	Conflict
}

public sealed record UpdateLeagueSeasonEventError(UpdateLeagueSeasonEventErrorCode Code, string Message);