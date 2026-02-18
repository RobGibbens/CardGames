using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.IngestLeagueSeasonEventResults;

public sealed record IngestLeagueSeasonEventResultsCommand(
	Guid LeagueId,
	Guid SeasonId,
	Guid EventId,
	IngestLeagueSeasonEventResultsRequest Request)
	: IRequest<OneOf<Unit, IngestLeagueSeasonEventResultsError>>;

public enum IngestLeagueSeasonEventResultsErrorCode
{
	Unauthorized,
	Forbidden,
	LeagueNotFound,
	SeasonNotFound,
	EventNotFound,
	MismatchedLeagueOrSeason,
	ResultsAlreadyIngested,
	InvalidRequest,
	MemberNotFound
}

public sealed record IngestLeagueSeasonEventResultsError(IngestLeagueSeasonEventResultsErrorCode Code, string Message);
