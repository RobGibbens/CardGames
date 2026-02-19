using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CorrectLeagueSeasonEventResults;

public sealed record CorrectLeagueSeasonEventResultsCommand(
	Guid LeagueId,
	Guid SeasonId,
	Guid EventId,
	CorrectLeagueSeasonEventResultsRequest Request)
	: IRequest<OneOf<Unit, CorrectLeagueSeasonEventResultsError>>;

public enum CorrectLeagueSeasonEventResultsErrorCode
{
	Unauthorized,
	Forbidden,
	LeagueNotFound,
	SeasonNotFound,
	EventNotFound,
	MismatchedLeagueOrSeason,
	ResultsNotIngested,
	InvalidRequest,
	MemberNotFound
}

public sealed record CorrectLeagueSeasonEventResultsError(CorrectLeagueSeasonEventResultsErrorCode Code, string Message);
