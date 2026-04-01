namespace CardGames.Poker.Api.Infrastructure.Diagnostics;

/// <summary>
/// Ambient (AsyncLocal) counter for SQL command executions per logical request.
/// Register as Singleton; the AsyncLocal provides per-request isolation.
/// Call <see cref="Start"/> at the beginning of a request and read <see cref="CommandCount"/> at the end.
/// </summary>
public sealed class QueryCountContext
{
	private static readonly AsyncLocal<int> _commandCount = new();

	public int CommandCount => _commandCount.Value;

	public void Increment() => _commandCount.Value++;

	/// <summary>Resets the counter for a new request scope.</summary>
	public void Reset() => _commandCount.Value = 0;
}
