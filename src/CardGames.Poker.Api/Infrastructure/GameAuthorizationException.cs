namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
///     Raised by <see cref="PipelineBehaviors.GameAuthorizationBehavior{TRequest,TResponse}" />
///     when an HTTP caller is not allowed to run a game command.
/// </summary>
public sealed class GameAuthorizationException(int statusCode, string code, string message) : Exception(message)
{
	public int StatusCode { get; } = statusCode;

	public string Code { get; } = code;
}
