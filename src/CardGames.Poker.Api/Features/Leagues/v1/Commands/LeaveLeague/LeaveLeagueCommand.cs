using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.LeaveLeague;

public sealed record LeaveLeagueCommand(Guid LeagueId)
	: IRequest<OneOf<LeaveLeagueResponse, LeaveLeagueError>>;

public enum LeaveLeagueErrorCode
{
	Unauthorized,
	Conflict
}

public sealed record LeaveLeagueError(LeaveLeagueErrorCode Code, string Message);