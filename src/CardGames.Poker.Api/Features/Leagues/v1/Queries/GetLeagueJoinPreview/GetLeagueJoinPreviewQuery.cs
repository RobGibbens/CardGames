using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries.GetLeagueJoinPreview;

public sealed record GetLeagueJoinPreviewQuery(string Token)
	: IRequest<OneOf<LeagueJoinPreviewDto, GetLeagueJoinPreviewError>>;

public enum GetLeagueJoinPreviewErrorCode
{
	Unauthorized,
	InvalidInvite
}

public sealed record GetLeagueJoinPreviewError(GetLeagueJoinPreviewErrorCode Code, string Message);