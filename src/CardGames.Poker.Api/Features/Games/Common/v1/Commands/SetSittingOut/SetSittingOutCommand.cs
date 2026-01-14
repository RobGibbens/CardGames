using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.SetSittingOut;

public record SetSittingOutCommand(Guid GameId, bool IsSittingOut)
	: IRequest<OneOf<bool, string>>;

