using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.JoinLeague;

public sealed record JoinLeagueCommand(JoinLeagueRequest Request)
	: IRequest<OneOf<JoinLeagueResponse, JoinLeagueError>>;

public enum JoinLeagueErrorCode
{
	Unauthorized,
	InvalidInvite,
	InviteRevoked,
	InviteExpired
}

public sealed record JoinLeagueError(JoinLeagueErrorCode Code, string Message);