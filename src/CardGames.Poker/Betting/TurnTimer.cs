using System;
using System.Threading;
using System.Threading.Tasks;

namespace CardGames.Poker.Betting;

/// <summary>
/// Manages the turn timer for betting actions.
/// </summary>
public class TurnTimer : IDisposable
{
    private readonly int _timeoutSeconds;
    private readonly int _warningSeconds;
    private CancellationTokenSource _cts;
    private Task _timerTask;
    private bool _disposed;

    /// <summary>
    /// Event raised when the turn times out.
    /// </summary>
    public event Action OnTimeout;

    /// <summary>
    /// Event raised as a warning before timeout.
    /// </summary>
    public event Action<int> OnWarning;

    /// <summary>
    /// Gets the default timeout in seconds.
    /// </summary>
    public int TimeoutSeconds => _timeoutSeconds;

    /// <summary>
    /// Gets the time remaining until timeout.
    /// </summary>
    public int SecondsRemaining { get; private set; }

    /// <summary>
    /// Gets whether the timer is currently running.
    /// </summary>
    public bool IsRunning => _timerTask != null && !_timerTask.IsCompleted;

    /// <summary>
    /// Creates a new turn timer.
    /// </summary>
    /// <param name="timeoutSeconds">The timeout duration in seconds.</param>
    /// <param name="warningSeconds">When to raise a warning (seconds remaining).</param>
    public TurnTimer(int timeoutSeconds, int warningSeconds = 10)
    {
        _timeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : 30;
        _warningSeconds = warningSeconds > 0 ? warningSeconds : 10;
        SecondsRemaining = _timeoutSeconds;
    }

    /// <summary>
    /// Starts the turn timer.
    /// </summary>
    public void Start()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TurnTimer));
        }

        Stop();

        SecondsRemaining = _timeoutSeconds;
        _cts = new CancellationTokenSource();
        _timerTask = RunTimerAsync(_cts.Token);
    }

    /// <summary>
    /// Stops the turn timer.
    /// </summary>
    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
        _timerTask = null;
        SecondsRemaining = _timeoutSeconds;
    }

    /// <summary>
    /// Resets the turn timer to the full duration.
    /// </summary>
    public void Reset()
    {
        Stop();
        Start();
    }

    /// <summary>
    /// Adds time to the current turn.
    /// </summary>
    /// <param name="seconds">The number of seconds to add.</param>
    public void AddTime(int seconds)
    {
        if (seconds > 0)
        {
            SecondsRemaining += seconds;
        }
    }

    private async Task RunTimerAsync(CancellationToken cancellationToken)
    {
        var warningRaised = false;

        try
        {
            while (SecondsRemaining > 0 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
                SecondsRemaining--;

                if (!warningRaised && SecondsRemaining <= _warningSeconds && SecondsRemaining > 0)
                {
                    warningRaised = true;
                    OnWarning?.Invoke(SecondsRemaining);
                }
            }

            if (SecondsRemaining <= 0 && !cancellationToken.IsCancellationRequested)
            {
                OnTimeout?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
            // Timer was stopped - this is expected
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration for turn timer behavior.
/// </summary>
public class TurnTimerConfig
{
    /// <summary>
    /// Default timeout in seconds when a player doesn't act.
    /// </summary>
    public int DefaultTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Warning threshold in seconds before timeout.
    /// </summary>
    public int WarningThresholdSeconds { get; init; } = 10;

    /// <summary>
    /// Time bank available to each player per session.
    /// </summary>
    public int TimeBankSeconds { get; init; } = 60;

    /// <summary>
    /// Whether to automatically apply default action on timeout.
    /// </summary>
    public bool AutoActOnTimeout { get; init; } = true;

    /// <summary>
    /// Default configuration for cash games.
    /// </summary>
    public static TurnTimerConfig CashGame { get; } = new()
    {
        DefaultTimeoutSeconds = 30,
        WarningThresholdSeconds = 10,
        TimeBankSeconds = 60,
        AutoActOnTimeout = true
    };

    /// <summary>
    /// Default configuration for tournaments.
    /// </summary>
    public static TurnTimerConfig Tournament { get; } = new()
    {
        DefaultTimeoutSeconds = 15,
        WarningThresholdSeconds = 5,
        TimeBankSeconds = 30,
        AutoActOnTimeout = true
    };

    /// <summary>
    /// Disabled timer configuration (no timeout).
    /// </summary>
    public static TurnTimerConfig Disabled { get; } = new()
    {
        DefaultTimeoutSeconds = 0,
        WarningThresholdSeconds = 0,
        TimeBankSeconds = 0,
        AutoActOnTimeout = false
    };
}
