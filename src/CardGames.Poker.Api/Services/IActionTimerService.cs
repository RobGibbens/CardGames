namespace CardGames.Poker.Api.Services;

/// <summary>
/// Service for managing action timers for player turns.
/// </summary>
public interface IActionTimerService
{
    /// <summary>
    /// The default timer duration in seconds.
    /// </summary>
    const int DefaultTimerDurationSeconds = 60;

    /// <summary>
    /// Starts or restarts the action timer for a game.
    /// </summary>
    /// <param name="gameId">The game ID.</param>
    /// <param name="playerSeatIndex">The seat index of the player whose turn it is.</param>
    /// <param name="durationSeconds">The timer duration in seconds.</param>
    /// <param name="onExpired">Callback invoked when the timer expires.</param>
    void StartTimer(Guid gameId, int playerSeatIndex, int durationSeconds = DefaultTimerDurationSeconds, Func<Guid, int, Task>? onExpired = null);

    /// <summary>
    /// Stops the action timer for a game.
    /// </summary>
    /// <param name="gameId">The game ID.</param>
    void StopTimer(Guid gameId);

    /// <summary>
    /// Gets the current timer state for a game.
    /// </summary>
    /// <param name="gameId">The game ID.</param>
    /// <returns>The timer state, or null if no timer is active.</returns>
    ActionTimerState? GetTimerState(Guid gameId);

    /// <summary>
    /// Checks if a timer is active for a game.
    /// </summary>
    /// <param name="gameId">The game ID.</param>
    /// <returns>True if a timer is active.</returns>
    bool IsTimerActive(Guid gameId);
}

/// <summary>
/// Represents the state of an action timer.
/// </summary>
public sealed record ActionTimerState
{
    /// <summary>
    /// The game ID.
    /// </summary>
    public required Guid GameId { get; init; }

    /// <summary>
    /// The seat index of the player whose turn it is.
    /// </summary>
    public required int PlayerSeatIndex { get; init; }

    /// <summary>
    /// The total duration of the timer in seconds.
    /// </summary>
    public required int DurationSeconds { get; init; }

    /// <summary>
    /// The UTC timestamp when the timer was started.
    /// </summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>
    /// The number of seconds remaining on the timer.
    /// </summary>
    public int SecondsRemaining
    {
        get
        {
            var elapsed = DateTimeOffset.UtcNow - StartedAtUtc;
            var remaining = DurationSeconds - (int)elapsed.TotalSeconds;
            return Math.Max(0, remaining);
        }
    }

    /// <summary>
    /// Whether the timer has expired.
    /// </summary>
    public bool IsExpired => SecondsRemaining <= 0;
}
