using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueSeasonEvents;

public sealed record GetLeagueSeasonEventsQuery(Guid LeagueId, Guid SeasonId)
	: IRequest<OneOf<IReadOnlyList<LeagueSeasonEventDto>, GetLeagueSeasonEventsError>>;

public enum GetLeagueSeasonEventsErrorCode
{
	Unauthorized,
	Forbidden,
	SeasonNotFound
}

public sealed record GetLeagueSeasonEventsError(GetLeagueSeasonEventsErrorCode Code, string Message);