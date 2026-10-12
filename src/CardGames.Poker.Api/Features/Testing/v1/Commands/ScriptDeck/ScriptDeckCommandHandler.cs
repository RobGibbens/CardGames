using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Testing.v1.Commands.ScriptDeck;

public sealed class ScriptDeckCommandHandler(IScriptedDeckStore decks)
	: IRequestHandler<ScriptDeckCommand, OneOf<ScriptDeckSuccessful, ScriptDeckError>>
{
	public Task<OneOf<ScriptDeckSuccessful, ScriptDeckError>> Handle(
		ScriptDeckCommand request,
		CancellationToken cancellationToken)
	{
		if (!ScriptedDeckStore.TryParse(request.Cards, out var deck, out var message))
			return Task.FromResult<OneOf<ScriptDeckSuccessful, ScriptDeckError>>(
				new ScriptDeckError(ScriptDeckErrorCode.InvalidDeck, message));

		decks.Set(request.GameId, deck);
		return Task.FromResult<OneOf<ScriptDeckSuccessful, ScriptDeckError>>(new ScriptDeckSuccessful());
	}
}
