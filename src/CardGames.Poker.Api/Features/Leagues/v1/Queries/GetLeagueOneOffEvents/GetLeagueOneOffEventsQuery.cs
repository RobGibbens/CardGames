using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueOneOffEvents;

public sealed record GetLeagueOneOffEventsQuery(Guid LeagueId)
	: IRequest<OneOf<IReadOnlyList<LeagueOneOffEventDto>, GetLeagueOneOffEventsError>>;

public enum GetLeagueOneOffEventsErrorCode
{
	Unauthorized,
	Forbidden
}

public sealed record GetLeagueOneOffEventsError(GetLeagueOneOffEventsErrorCode Code, string Message);