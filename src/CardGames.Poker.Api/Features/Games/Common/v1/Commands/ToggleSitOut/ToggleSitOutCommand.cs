using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleSitOut;

public sealed record ToggleSitOutCommand(Guid GameId, bool IsSittingOut)
	: IRequest<OneOf<ToggleSitOutSuccessful, ToggleSitOutError>>,
	  IGameStateChangingCommand;
