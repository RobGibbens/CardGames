using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueMembershipHistory;

public sealed record GetLeagueMembershipHistoryQuery(Guid LeagueId)
	: IRequest<OneOf<IReadOnlyList<LeagueMembershipHistoryItemDto>, GetLeagueMembershipHistoryError>>;

public enum GetLeagueMembershipHistoryErrorCode
{
	Unauthorized,
	Forbidden
}

public sealed record GetLeagueMembershipHistoryError(GetLeagueMembershipHistoryErrorCode Code, string Message);
