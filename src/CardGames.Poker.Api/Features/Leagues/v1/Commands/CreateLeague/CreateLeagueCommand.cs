using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeague;

public sealed record CreateLeagueCommand(CreateLeagueRequest Request)
	: IRequest<OneOf<CreateLeagueResponse, CreateLeagueError>>;

public enum CreateLeagueErrorCode
{
	Unauthorized,
	InvalidRequest
}

public sealed record CreateLeagueError(CreateLeagueErrorCode Code, string Message);