using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Testing.v1.Commands.ScriptDeck;

/// <summary>
///     Stores the card order for the next initial deal of a game.
/// </summary>
public sealed record ScriptDeckCommand(Guid GameId, IReadOnlyList<string>? Cards)
	: IRequest<OneOf<ScriptDeckSuccessful, ScriptDeckError>>;

public sealed record ScriptDeckSuccessful;

public enum ScriptDeckErrorCode
{
	InvalidDeck
}

public sealed record ScriptDeckError(ScriptDeckErrorCode Code, string Message);
