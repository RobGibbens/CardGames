using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeason;

public sealed record CreateLeagueSeasonCommand(Guid LeagueId, CreateLeagueSeasonRequest Request)
	: IRequest<OneOf<CreateLeagueSeasonResponse, CreateLeagueSeasonError>>;

public enum CreateLeagueSeasonErrorCode
{
	Unauthorized,
	Forbidden,
	LeagueNotFound,
	InvalidRequest
}

public sealed record CreateLeagueSeasonError(CreateLeagueSeasonErrorCode Code, string Message);