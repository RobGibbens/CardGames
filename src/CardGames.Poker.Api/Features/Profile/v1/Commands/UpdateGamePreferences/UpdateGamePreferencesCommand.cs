using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Profile.v1.Commands.UpdateGamePreferences;

public sealed record UpdateGamePreferencesCommand(
	int DefaultSmallBlind,
	int DefaultBigBlind,
	int DefaultAnte,
	int DefaultMinimumBet)
	: IRequest<OneOf<GamePreferencesDto, UpdateGamePreferencesError>>;

public enum UpdateGamePreferencesErrorCode
{
	Unauthorized,
	InvalidPreferences
}

public sealed record UpdateGamePreferencesError(UpdateGamePreferencesErrorCode Code, string Message);
