#nullable enable
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CardGames.Poker.Betting;

/// <summary>
/// Service for managing player action timers with time bank support.
/// </summary>
public class TimerService : ITimerService, IDisposable
{
    private readonly TurnTimerConfig _config;
    private readonly ConcurrentDictionary<string, int> _playerTimeBanks = new();
    private readonly Func<string, BettingActionType>? _defaultActionProvider;
    private readonly object _timerLock = new();
    
    private CancellationTokenSource? _cts;
    private Task? _timerTask;
    private string? _currentPlayerName;
    private int _secondsRemaining;
    private bool _warningRaised;
    private bool _timeBankActive;
    private bool _disposed;

    /// <inheritdoc />
    public TurnTimerConfig Config => _config;

    /// <inheritdoc />
    public bool IsRunning
    {
        get
        {
            lock (_timerLock)
            {
                return _timerTask != null && !_timerTask.IsCompleted;
            }
        }
    }

    /// <inheritdoc />
    public string? CurrentPlayerName
    {
        get
        {
            lock (_timerLock)
            {
                return _currentPlayerName;
            }
        }
    }

    /// <inheritdoc />
    public int SecondsRemaining
    {
        get
        {
            lock (_timerLock)
            {
                return _secondsRemaining;
            }
        }
    }

    /// <inheritdoc />
    public event Action<TimerStartedEventArgs>? OnTimerStarted;
    
    /// <inheritdoc />
    public event Action<TimerTickEventArgs>? OnTimerTick;
    
    /// <inheritdoc />
    public event Action<TimerWarningEventArgs>? OnTimerWarning;
    
    /// <inheritdoc />
    public event Action<TimerExpiredEventArgs>? OnTimerExpired;
    
    /// <inheritdoc />
    public event Action<TimeBankUsedEventArgs>? OnTimeBankUsed;

    /// <summary>
    /// Creates a new timer service with the specified configuration.
    /// </summary>
    /// <param name="config">Timer configuration.</param>
    /// <param name="defaultActionProvider">Optional function to determine the default action for a player when timer expires.</param>
    public TimerService(TurnTimerConfig? config = null, Func<string, BettingActionType>? defaultActionProvider = null)
    {
        _config = config ?? TurnTimerConfig.CashGame;
        _defaultActionProvider = defaultActionProvider;
    }

    /// <inheritdoc />
    public int GetTimeBankRemaining(string playerName)
    {
        return _playerTimeBanks.TryGetValue(playerName, out var value) ? value : _config.TimeBankSeconds;
    }

    /// <inheritdoc />
    public void StartTimer(string playerName)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TimerService));
        }

        if (_config.DefaultTimeoutSeconds <= 0)
        {
            return; // Timer is disabled
        }

        lock (_timerLock)
        {
            // Stop any existing timer
            StopTimerInternal();

            _currentPlayerName = playerName;
            _secondsRemaining = _config.DefaultTimeoutSeconds;
            _warningRaised = false;
            _timeBankActive = false;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var timeBankRemaining = GetTimeBankRemaining(playerName);
            
            OnTimerStarted?.Invoke(new TimerStartedEventArgs(
                playerName, 
                _config.DefaultTimeoutSeconds, 
                timeBankRemaining));

            _timerTask = RunTimerAsync(token);
        }
    }

    /// <inheritdoc />
    public void StopTimer()
    {
        lock (_timerLock)
        {
            StopTimerInternal();
        }
    }

    /// <inheritdoc />
    public void ResetTimer()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TimerService));
        }

        lock (_timerLock)
        {
            if (_currentPlayerName != null)
            {
                var playerName = _currentPlayerName;
                StopTimerInternal();
                
                // Restart with the same player
                _currentPlayerName = playerName;
                _secondsRemaining = _config.DefaultTimeoutSeconds;
                _warningRaised = false;
                _timeBankActive = false;

                _cts = new CancellationTokenSource();
                var token = _cts.Token;

                var timeBankRemaining = GetTimeBankRemaining(playerName);
                
                OnTimerStarted?.Invoke(new TimerStartedEventArgs(
                    playerName, 
                    _config.DefaultTimeoutSeconds, 
                    timeBankRemaining));

                _timerTask = RunTimerAsync(token);
            }
        }
    }

    /// <inheritdoc />
    public bool UseTimeBank(string playerName)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TimerService));
        }

        lock (_timerLock)
        {
            // Can only use time bank for the current player whose timer is running
            if (_currentPlayerName != playerName || !IsRunning)
            {
                return false;
            }

            // If time bank is already active, can't activate again
            if (_timeBankActive)
            {
                return false;
            }

            var timeBankRemaining = GetTimeBankRemaining(playerName);
            if (timeBankRemaining <= 0)
            {
                return false;
            }

            // Add time from the time bank
            var secondsToAdd = Math.Min(timeBankRemaining, _config.TimeBankSeconds);
            _secondsRemaining += secondsToAdd;
            _timeBankActive = true;

            // Update the player's time bank
            _playerTimeBanks[playerName] = timeBankRemaining - secondsToAdd;

            OnTimeBankUsed?.Invoke(new TimeBankUsedEventArgs(
                playerName, 
                secondsToAdd, 
                _playerTimeBanks[playerName]));

            return true;
        }
    }

    /// <inheritdoc />
    public void ResetAllTimeBanks()
    {
        _playerTimeBanks.Clear();
    }

    /// <inheritdoc />
    public void InitializePlayerTimeBank(string playerName)
    {
        _playerTimeBanks[playerName] = _config.TimeBankSeconds;
    }

    /// <inheritdoc />
    public void RemovePlayer(string playerName)
    {
        _playerTimeBanks.TryRemove(playerName, out _);
    }

    private void StopTimerInternal()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
        _timerTask = null;
        _currentPlayerName = null;
        _secondsRemaining = 0;
        _warningRaised = false;
        _timeBankActive = false;
    }

    private async Task RunTimerAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int currentSeconds;
                string? currentPlayer;
                bool shouldRaiseWarning = false;
                bool shouldExpire = false;

                lock (_timerLock)
                {
                    currentSeconds = _secondsRemaining;
                    currentPlayer = _currentPlayerName;
                    
                    if (currentSeconds <= 0 || currentPlayer == null)
                    {
                        shouldExpire = currentPlayer != null && currentSeconds <= 0;
                        break;
                    }
                }

                // Wait one second
                await Task.Delay(1000, cancellationToken);

                lock (_timerLock)
                {
                    if (cancellationToken.IsCancellationRequested || _currentPlayerName == null)
                    {
                        return;
                    }

                    _secondsRemaining--;
                    currentSeconds = _secondsRemaining;
                    currentPlayer = _currentPlayerName;

                    // Check for warning
                    if (!_warningRaised && currentSeconds <= _config.WarningThresholdSeconds && currentSeconds > 0)
                    {
                        _warningRaised = true;
                        shouldRaiseWarning = true;
                    }

                    shouldExpire = currentSeconds <= 0;
                }

                // Raise events outside the lock to avoid deadlocks
                if (currentPlayer != null)
                {
                    OnTimerTick?.Invoke(new TimerTickEventArgs(currentPlayer, currentSeconds));

                    if (shouldRaiseWarning)
                    {
                        OnTimerWarning?.Invoke(new TimerWarningEventArgs(currentPlayer, currentSeconds));
                    }

                    if (shouldExpire)
                    {
                        HandleExpiration(currentPlayer);
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timer was stopped - this is expected
        }
    }

    private void HandleExpiration(string playerName)
    {
        if (!_config.AutoActOnTimeout)
        {
            OnTimerExpired?.Invoke(new TimerExpiredEventArgs(playerName, BettingActionType.Fold));
            return;
        }

        // Determine default action
        BettingActionType defaultAction;
        if (_defaultActionProvider != null)
        {
            defaultAction = _defaultActionProvider(playerName);
        }
        else
        {
            // Default to fold (safest default action)
            defaultAction = BettingActionType.Fold;
        }

        OnTimerExpired?.Invoke(new TimerExpiredEventArgs(playerName, defaultAction));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_timerLock)
            {
                StopTimerInternal();
            }
            _disposed = true;
        }
    }
}
