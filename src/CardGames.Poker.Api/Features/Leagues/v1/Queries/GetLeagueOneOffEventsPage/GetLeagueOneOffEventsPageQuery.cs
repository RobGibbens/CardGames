using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueOneOffEventsPage;

public sealed record GetLeagueOneOffEventsPageQuery(Guid LeagueId, int PageSize, int PageNumber)
	: IRequest<OneOf<LeagueOneOffEventsPageDto, GetLeagueOneOffEventsPageError>>;

public enum GetLeagueOneOffEventsPageErrorCode
{
	Unauthorized,
	Forbidden
}

public sealed record GetLeagueOneOffEventsPageError(GetLeagueOneOffEventsPageErrorCode Code, string Message);