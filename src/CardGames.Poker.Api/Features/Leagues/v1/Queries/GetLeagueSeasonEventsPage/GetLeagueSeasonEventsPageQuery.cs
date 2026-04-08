using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueSeasonEventsPage;

public sealed record GetLeagueSeasonEventsPageQuery(Guid LeagueId, Guid SeasonId, int PageSize, int PageNumber)
	: IRequest<OneOf<LeagueSeasonEventsPageDto, GetLeagueSeasonEventsPageError>>;

public enum GetLeagueSeasonEventsPageErrorCode
{
	Unauthorized,
	Forbidden,
	SeasonNotFound
}

public sealed record GetLeagueSeasonEventsPageError(GetLeagueSeasonEventsPageErrorCode Code, string Message);