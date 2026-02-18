using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.LaunchLeagueEventSession;

public enum LeagueEventSourceType
{
	Season = 1,
	OneOff = 2
}

public sealed record LaunchLeagueEventSessionCommand(
	Guid LeagueId,
	LeagueEventSourceType SourceType,
	Guid EventId,
	Guid? SeasonId,
	LaunchLeagueEventSessionRequest Request)
	: IRequest<OneOf<LaunchLeagueEventSessionResponse, LaunchLeagueEventSessionError>>;

public enum LaunchLeagueEventSessionErrorCode
{
	Unauthorized,
	Forbidden,
	InvalidRequest,
	LeagueNotFound,
	EventNotFound,
	MismatchedLeagueOrSeason,
	AlreadyLaunched,
	CreateGameConflict
}

public sealed record LaunchLeagueEventSessionError(LaunchLeagueEventSessionErrorCode Code, string Message);
