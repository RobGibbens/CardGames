using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueStandings;

public sealed record GetLeagueStandingsQuery(Guid LeagueId)
	: IRequest<OneOf<IReadOnlyList<LeagueStandingEntryDto>, GetLeagueStandingsError>>;

public enum GetLeagueStandingsErrorCode
{
	Unauthorized,
	Forbidden
}

public sealed record GetLeagueStandingsError(GetLeagueStandingsErrorCode Code, string Message);
