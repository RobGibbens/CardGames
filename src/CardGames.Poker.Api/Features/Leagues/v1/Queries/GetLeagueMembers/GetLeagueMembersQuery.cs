using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueMembers;

public sealed record GetLeagueMembersQuery(Guid LeagueId)
	: IRequest<OneOf<IReadOnlyList<LeagueMemberDto>, GetLeagueMembersError>>;

public enum GetLeagueMembersErrorCode
{
	Unauthorized,
	Forbidden
}

public sealed record GetLeagueMembersError(GetLeagueMembersErrorCode Code, string Message);