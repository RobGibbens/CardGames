using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.DemoteLeagueAdminToMember;

public sealed record DemoteLeagueAdminToMemberCommand(Guid LeagueId, string MemberUserId)
	: IRequest<OneOf<Unit, DemoteLeagueAdminToMemberError>>;

public enum DemoteLeagueAdminToMemberErrorCode
{
	Unauthorized,
	Forbidden,
	MemberNotFound,
	InvalidRequest,
	Conflict
}

public sealed record DemoteLeagueAdminToMemberError(DemoteLeagueAdminToMemberErrorCode Code, string Message);
