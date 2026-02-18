using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.DenyLeagueJoinRequest;

public sealed record DenyLeagueJoinRequestCommand(Guid LeagueId, Guid JoinRequestId, ModerateLeagueJoinRequestRequest Request)
	: IRequest<OneOf<Unit, DenyLeagueJoinRequestError>>;

public enum DenyLeagueJoinRequestErrorCode
{
	Unauthorized,
	Forbidden,
	JoinRequestNotFound,
	InvalidState
}

public sealed record DenyLeagueJoinRequestError(DenyLeagueJoinRequestErrorCode Code, string Message);
