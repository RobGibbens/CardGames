using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueSeasonEvent;

public sealed record CreateLeagueSeasonEventCommand(Guid LeagueId, Guid SeasonId, CreateLeagueSeasonEventRequest Request)
	: IRequest<OneOf<CreateLeagueSeasonEventResponse, CreateLeagueSeasonEventError>>;

public enum CreateLeagueSeasonEventErrorCode
{
	Unauthorized,
	Forbidden,
	LeagueNotFound,
	SeasonNotFound,
	InvalidRequest
}

public sealed record CreateLeagueSeasonEventError(CreateLeagueSeasonEventErrorCode Code, string Message);