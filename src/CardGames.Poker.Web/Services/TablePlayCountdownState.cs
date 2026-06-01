using CardGames.Contracts.SignalR;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Circuit-scoped state service that owns the table page's "next hand" / "return to lobby"
/// countdown: the server-authored deadline, the client/server clock offset, the seconds
/// remaining, and the pure time math that drives them. The table component delegates these
/// computations here so that the page code-behind stays focused on gameplay orchestration
/// (timer lifecycle, navigation, overlay visibility).
/// Modelled after <see cref="TablePlayAudioState"/>.
/// </summary>
public sealed class TablePlayCountdownState
{
    /// <summary>
    /// How long the completed-game overlay stays up before returning the player to the lobby,
    /// when the server has not supplied an explicit next-hand deadline.
    /// </summary>
    public const int CompletedGameReturnToLobbyDurationSeconds = 10;

    private DateTimeOffset? _deadlineUtc;
    private TimeSpan _serverClockOffset;

    /// <summary>
    /// The whole seconds remaining until the current deadline, clamped at zero. Refreshed via
    /// <see cref="SyncWithServer"/> and <see cref="RefreshSeconds"/>.
    /// </summary>
    public int SecondsUntilNextHand { get; private set; }

    /// <summary>
    /// Whether a countdown deadline is currently set.
    /// </summary>
    public bool HasDeadline => _deadlineUtc.HasValue;

    /// <summary>
    /// Re-derives the clock offset and deadline from a fresh server-authored table state and
    /// recomputes <see cref="SecondsUntilNextHand"/>.
    /// </summary>
    public void SyncWithServer(TableStatePublicDto state, bool isEndedPhase, bool isGameCompleted)
    {
        ArgumentNullException.ThrowIfNull(state);

        _serverClockOffset = state.ServerUtcNow - DateTimeOffset.UtcNow;
        _deadlineUtc = ResolveDeadlineUtc(state, isEndedPhase, isGameCompleted);
        RefreshSeconds();
    }

    /// <summary>
    /// Clears the deadline and resets the remaining seconds to zero.
    /// </summary>
    public void Reset()
    {
        _deadlineUtc = null;
        SecondsUntilNextHand = 0;
    }

    /// <summary>
    /// Recomputes <see cref="SecondsUntilNextHand"/> from the current deadline and clock offset.
    /// </summary>
    public void RefreshSeconds()
    {
        SecondsUntilNextHand = CalculateSecondsUntilDeadline(_deadlineUtc, DateTimeOffset.UtcNow, _serverClockOffset);
    }

    /// <summary>
    /// The due time for the next one-second timer tick, aligned to the next whole-second boundary
    /// of the deadline. Returns <see cref="Timeout.InfiniteTimeSpan"/> when no deadline is set.
    /// </summary>
    public TimeSpan GetTimerDueTime()
    {
        if (!_deadlineUtc.HasValue)
        {
            return Timeout.InfiniteTimeSpan;
        }

        var adjustedServerNow = DateTimeOffset.UtcNow + _serverClockOffset;
        var remaining = _deadlineUtc.Value - adjustedServerNow;

        if (remaining <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var millisecondsUntilNextBoundary = remaining.TotalMilliseconds % 1000;
        if (millisecondsUntilNextBoundary < 1)
        {
            millisecondsUntilNextBoundary = 1000;
        }

        return TimeSpan.FromMilliseconds(millisecondsUntilNextBoundary);
    }

    /// <summary>
    /// Resolves the countdown deadline from server-authored table state: the explicit next-hand
    /// start time when present, otherwise the completed-hand timestamp plus the return-to-lobby
    /// grace period when the game has ended.
    /// </summary>
    public static DateTimeOffset? ResolveDeadlineUtc(TableStatePublicDto state, bool isEndedPhase, bool isGameCompleted)
    {
        if (state.NextHandStartsAtUtc.HasValue)
        {
            return state.NextHandStartsAtUtc.Value;
        }

        if ((isEndedPhase || isGameCompleted) && state.HandCompletedAtUtc.HasValue)
        {
            return state.HandCompletedAtUtc.Value.AddSeconds(CompletedGameReturnToLobbyDurationSeconds);
        }

        return null;
    }

    /// <summary>
    /// Computes the whole seconds remaining until <paramref name="deadlineUtc"/>, adjusting the
    /// client clock by <paramref name="serverClockOffset"/> and clamping the result at zero.
    /// </summary>
    public static int CalculateSecondsUntilDeadline(DateTimeOffset? deadlineUtc, DateTimeOffset clientUtcNow, TimeSpan serverClockOffset)
    {
        if (!deadlineUtc.HasValue)
        {
            return 0;
        }

        var adjustedServerNow = clientUtcNow + serverClockOffset;
        var remaining = deadlineUtc.Value - adjustedServerNow;
        return Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
    }
}
