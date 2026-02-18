using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetMyLeagues;

public sealed record GetMyLeaguesQuery : IRequest<OneOf<IReadOnlyList<LeagueSummaryDto>, GetMyLeaguesError>>;

public enum GetMyLeaguesErrorCode
{
	Unauthorized
}

public sealed record GetMyLeaguesError(GetMyLeaguesErrorCode Code, string Message);