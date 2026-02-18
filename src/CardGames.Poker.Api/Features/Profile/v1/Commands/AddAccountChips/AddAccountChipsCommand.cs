using CardGames.Poker.Api.Contracts;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Profile.v1.Commands.AddAccountChips;

public sealed record AddAccountChipsCommand(int Amount, string? Reason)
	: IRequest<OneOf<AddAccountChipsResponse, AddAccountChipsError>>;

public enum AddAccountChipsErrorCode
{
	Unauthorized,
	InvalidAmount,
	PlayerUnavailable
}

public sealed record AddAccountChipsError(AddAccountChipsErrorCode Code, string Message);
