using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueSeasons;

public sealed record GetLeagueSeasonsQuery(Guid LeagueId)
	: IRequest<OneOf<IReadOnlyList<LeagueSeasonDto>, GetLeagueSeasonsError>>;

public enum GetLeagueSeasonsErrorCode
{
	Unauthorized,
	Forbidden
}

public sealed record GetLeagueSeasonsError(GetLeagueSeasonsErrorCode Code, string Message);