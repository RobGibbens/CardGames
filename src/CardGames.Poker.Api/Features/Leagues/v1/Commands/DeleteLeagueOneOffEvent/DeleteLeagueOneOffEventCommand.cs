using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.DeleteLeagueOneOffEvent;

public sealed record DeleteLeagueOneOffEventCommand(Guid LeagueId, Guid EventId)
	: IRequest<OneOf<Unit, DeleteLeagueOneOffEventError>>;

public enum DeleteLeagueOneOffEventErrorCode
{
	Unauthorized,
	Forbidden,
	LeagueNotFound,
	EventNotFound,
	Conflict
}

public sealed record DeleteLeagueOneOffEventError(DeleteLeagueOneOffEventErrorCode Code, string Message);