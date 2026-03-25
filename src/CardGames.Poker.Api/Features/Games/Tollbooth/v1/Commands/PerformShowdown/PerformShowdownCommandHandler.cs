using MediatR;
using OneOf;
using SharedPerformShowdownCommand = CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownCommand;
using SharedPerformShowdownError = CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownError;
using SharedPerformShowdownSuccessful = CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownSuccessful;

namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.PerformShowdown;

public sealed class PerformShowdownCommandHandler(
	IRequestHandler<SharedPerformShowdownCommand, OneOf<SharedPerformShowdownSuccessful, SharedPerformShowdownError>> innerHandler)
	: IRequestHandler<PerformShowdownCommand, OneOf<SharedPerformShowdownSuccessful, SharedPerformShowdownError>>
{
	public Task<OneOf<SharedPerformShowdownSuccessful, SharedPerformShowdownError>> Handle(
		PerformShowdownCommand command,
		CancellationToken cancellationToken)
		=> innerHandler.Handle(new SharedPerformShowdownCommand(command.GameId), cancellationToken);
}
