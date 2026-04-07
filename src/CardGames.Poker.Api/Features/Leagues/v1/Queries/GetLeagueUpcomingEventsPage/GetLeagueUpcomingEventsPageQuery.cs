using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueUpcomingEventsPage;

public sealed record GetLeagueUpcomingEventsPageQuery(Guid LeagueId, int PageSize = 5, int PageNumber = 1)
	: IRequest<OneOf<LeagueUpcomingEventsPageDto, GetLeagueUpcomingEventsPageError>>;

public enum GetLeagueUpcomingEventsPageErrorCode
{
	Unauthorized,
	Forbidden
}

public sealed record GetLeagueUpcomingEventsPageError(GetLeagueUpcomingEventsPageErrorCode Code, string Message);