using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueInvite;

public sealed record CreateLeagueInviteCommand(Guid LeagueId, CreateLeagueInviteRequest Request)
	: IRequest<OneOf<CreateLeagueInviteResponse, CreateLeagueInviteError>>;

public enum CreateLeagueInviteErrorCode
{
	Unauthorized,
	Forbidden,
	LeagueNotFound,
	InvalidExpiry
}

public sealed record CreateLeagueInviteError(CreateLeagueInviteErrorCode Code, string Message);