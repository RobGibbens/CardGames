using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetPendingLeagueJoinRequests;

public sealed record GetPendingLeagueJoinRequestsQuery(Guid LeagueId)
	: IRequest<OneOf<IReadOnlyList<LeagueJoinRequestQueueItemDto>, GetPendingLeagueJoinRequestsError>>;

public enum GetPendingLeagueJoinRequestsErrorCode
{
	Unauthorized,
	Forbidden
}

public sealed record GetPendingLeagueJoinRequestsError(GetPendingLeagueJoinRequestsErrorCode Code, string Message);
