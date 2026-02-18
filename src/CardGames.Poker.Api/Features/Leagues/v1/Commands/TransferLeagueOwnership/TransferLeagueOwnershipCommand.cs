using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.TransferLeagueOwnership;

public sealed record TransferLeagueOwnershipCommand(Guid LeagueId, string MemberUserId)
	: IRequest<OneOf<Unit, TransferLeagueOwnershipError>>;

public enum TransferLeagueOwnershipErrorCode
{
	Unauthorized,
	Forbidden,
	MemberNotFound,
	InvalidRequest
}

public sealed record TransferLeagueOwnershipError(TransferLeagueOwnershipErrorCode Code, string Message);
