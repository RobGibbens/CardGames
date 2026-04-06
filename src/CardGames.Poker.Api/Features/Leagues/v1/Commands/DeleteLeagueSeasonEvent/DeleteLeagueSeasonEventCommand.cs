using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.DeleteLeagueSeasonEvent;

public sealed record DeleteLeagueSeasonEventCommand(Guid LeagueId, Guid SeasonId, Guid EventId)
	: IRequest<OneOf<Unit, DeleteLeagueSeasonEventError>>;

public enum DeleteLeagueSeasonEventErrorCode
{
	Unauthorized,
	Forbidden,
	LeagueNotFound,
	SeasonNotFound,
	EventNotFound,
	Conflict
}

public sealed record DeleteLeagueSeasonEventError(DeleteLeagueSeasonEventErrorCode Code, string Message);