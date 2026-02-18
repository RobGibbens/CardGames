using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.ApproveLeagueJoinRequest;

public sealed record ApproveLeagueJoinRequestCommand(Guid LeagueId, Guid JoinRequestId, ModerateLeagueJoinRequestRequest Request)
	: IRequest<OneOf<Unit, ApproveLeagueJoinRequestError>>;

public enum ApproveLeagueJoinRequestErrorCode
{
	Unauthorized,
	Forbidden,
	JoinRequestNotFound,
	InvalidState
}

public sealed record ApproveLeagueJoinRequestError(ApproveLeagueJoinRequestErrorCode Code, string Message);
