using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueInvites;

public sealed record GetLeagueInvitesQuery(Guid LeagueId)
	: IRequest<OneOf<IReadOnlyList<LeagueInviteSummaryDto>, GetLeagueInvitesError>>;

public enum GetLeagueInvitesErrorCode
{
	Unauthorized,
	Forbidden
}

public sealed record GetLeagueInvitesError(GetLeagueInvitesErrorCode Code, string Message);