using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueActiveGamesPage;

public sealed record GetLeagueActiveGamesPageQuery(Guid LeagueId, int PageSize = 5, int PageNumber = 1)
	: IRequest<OneOf<LeagueActiveGamesPageDto, GetLeagueActiveGamesPageError>>;

public enum GetLeagueActiveGamesPageErrorCode
{
	Unauthorized,
	Forbidden
}

public sealed record GetLeagueActiveGamesPageError(GetLeagueActiveGamesPageErrorCode Code, string Message);