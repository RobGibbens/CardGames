using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.RevokeLeagueInvite;

public sealed record RevokeLeagueInviteCommand(Guid LeagueId, Guid InviteId)
	: IRequest<OneOf<Unit, RevokeLeagueInviteError>>;

public enum RevokeLeagueInviteErrorCode
{
	Unauthorized,
	Forbidden,
	InviteNotFound
}

public sealed record RevokeLeagueInviteError(RevokeLeagueInviteErrorCode Code, string Message);