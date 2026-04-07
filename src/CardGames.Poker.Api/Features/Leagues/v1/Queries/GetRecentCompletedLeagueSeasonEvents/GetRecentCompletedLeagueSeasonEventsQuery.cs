using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetRecentCompletedLeagueSeasonEvents;

public sealed record GetRecentCompletedLeagueSeasonEventsQuery(Guid LeagueId, Guid SeasonId, int Take = 5)
	: IRequest<OneOf<IReadOnlyList<LeagueSeasonEventDto>, GetRecentCompletedLeagueSeasonEventsError>>;

public enum GetRecentCompletedLeagueSeasonEventsErrorCode
{
	Unauthorized,
	Forbidden,
	SeasonNotFound
}

public sealed record GetRecentCompletedLeagueSeasonEventsError(GetRecentCompletedLeagueSeasonEventsErrorCode Code, string Message);