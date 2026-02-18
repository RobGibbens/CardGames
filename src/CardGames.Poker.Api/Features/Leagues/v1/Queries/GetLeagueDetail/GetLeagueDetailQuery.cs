using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueDetail;

public sealed record GetLeagueDetailQuery(Guid LeagueId)
	: IRequest<OneOf<LeagueDetailDto, GetLeagueDetailError>>;

public enum GetLeagueDetailErrorCode
{
	Unauthorized,
	Forbidden,
	NotFound
}

public sealed record GetLeagueDetailError(GetLeagueDetailErrorCode Code, string Message);