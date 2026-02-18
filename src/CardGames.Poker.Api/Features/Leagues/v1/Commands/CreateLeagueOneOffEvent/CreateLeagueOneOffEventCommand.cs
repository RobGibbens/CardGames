using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Leagues.v1.Commands.CreateLeagueOneOffEvent;

public sealed record CreateLeagueOneOffEventCommand(Guid LeagueId, CreateLeagueOneOffEventRequest Request)
	: IRequest<OneOf<CreateLeagueOneOffEventResponse, CreateLeagueOneOffEventError>>;

public enum CreateLeagueOneOffEventErrorCode
{
	Unauthorized,
	Forbidden,
	LeagueNotFound,
	InvalidRequest
}

public sealed record CreateLeagueOneOffEventError(CreateLeagueOneOffEventErrorCode Code, string Message);