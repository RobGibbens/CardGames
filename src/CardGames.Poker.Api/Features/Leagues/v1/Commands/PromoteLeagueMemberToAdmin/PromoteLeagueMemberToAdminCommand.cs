using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.PromoteLeagueMemberToAdmin;

public sealed record PromoteLeagueMemberToAdminCommand(Guid LeagueId, string MemberUserId)
	: IRequest<OneOf<Unit, PromoteLeagueMemberToAdminError>>;

public enum PromoteLeagueMemberToAdminErrorCode
{
	Unauthorized,
	Forbidden,
	MemberNotFound
}

public sealed record PromoteLeagueMemberToAdminError(PromoteLeagueMemberToAdminErrorCode Code, string Message);