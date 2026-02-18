using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.RemoveLeagueMember;

public sealed record RemoveLeagueMemberCommand(Guid LeagueId, string MemberUserId)
	: IRequest<OneOf<Unit, RemoveLeagueMemberError>>;

public enum RemoveLeagueMemberErrorCode
{
	Unauthorized,
	Forbidden,
	MemberNotFound,
	InvalidRequest,
	Conflict
}

public sealed record RemoveLeagueMemberError(RemoveLeagueMemberErrorCode Code, string Message);
