#nullable enable
using System;

namespace CardGames.Poker.Betting;

/// <summary>
/// Service interface for managing player action timers.
/// </summary>
public interface ITimerService
{
    /// <summary>
    /// Gets the configuration for this timer service.
    /// </summary>
    TurnTimerConfig Config { get; }

    /// <summary>
    /// Gets whether a timer is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the current player whose timer is running.
    /// </summary>
    string? CurrentPlayerName { get; }

    /// <summary>
    /// Gets the seconds remaining on the current timer.
    /// </summary>
    int SecondsRemaining { get; }

    /// <summary>
    /// Gets the time bank remaining for a specific player.
    /// </summary>
    int GetTimeBankRemaining(string playerName);

    /// <summary>
    /// Starts the timer for a player's turn.
    /// </summary>
    /// <param name="playerName">The name of the player whose turn it is.</param>
    void StartTimer(string playerName);

    /// <summary>
    /// Stops the current timer.
    /// </summary>
    void StopTimer();

    /// <summary>
    /// Resets the current timer to the full duration.
    /// </summary>
    void ResetTimer();

    /// <summary>
    /// Uses time bank for the current player.
    /// </summary>
    /// <param name="playerName">The name of the player using time bank.</param>
    /// <returns>True if time bank was successfully activated; false if no time bank remaining.</returns>
    bool UseTimeBank(string playerName);

    /// <summary>
    /// Resets the time bank for all players (e.g., at start of session).
    /// </summary>
    void ResetAllTimeBanks();

    /// <summary>
    /// Initializes a player's time bank.
    /// </summary>
    /// <param name="playerName">The name of the player.</param>
    void InitializePlayerTimeBank(string playerName);

    /// <summary>
    /// Removes a player's time bank tracking.
    /// </summary>
    /// <param name="playerName">The name of the player.</param>
    void RemovePlayer(string playerName);

    /// <summary>
    /// Event raised when the timer starts.
    /// </summary>
    event Action<TimerStartedEventArgs>? OnTimerStarted;

    /// <summary>
    /// Event raised every second while the timer is running.
    /// </summary>
    event Action<TimerTickEventArgs>? OnTimerTick;

    /// <summary>
    /// Event raised when the timer reaches the warning threshold.
    /// </summary>
    event Action<TimerWarningEventArgs>? OnTimerWarning;

    /// <summary>
    /// Event raised when the timer expires.
    /// </summary>
    event Action<TimerExpiredEventArgs>? OnTimerExpired;

    /// <summary>
    /// Event raised when a player uses their time bank.
    /// </summary>
    event Action<TimeBankUsedEventArgs>? OnTimeBankUsed;
}

/// <summary>
/// Event arguments for when a timer starts.
/// </summary>
public class TimerStartedEventArgs : EventArgs
{
    public string PlayerName { get; }
    public int DurationSeconds { get; }
    public int TimeBankRemaining { get; }

    public TimerStartedEventArgs(string playerName, int durationSeconds, int timeBankRemaining)
    {
        PlayerName = playerName;
        DurationSeconds = durationSeconds;
        TimeBankRemaining = timeBankRemaining;
    }
}

/// <summary>
/// Event arguments for timer tick events.
/// </summary>
public class TimerTickEventArgs : EventArgs
{
    public string PlayerName { get; }
    public int SecondsRemaining { get; }

    public TimerTickEventArgs(string playerName, int secondsRemaining)
    {
        PlayerName = playerName;
        SecondsRemaining = secondsRemaining;
    }
}

/// <summary>
/// Event arguments for timer warning events.
/// </summary>
public class TimerWarningEventArgs : EventArgs
{
    public string PlayerName { get; }
    public int SecondsRemaining { get; }

    public TimerWarningEventArgs(string playerName, int secondsRemaining)
    {
        PlayerName = playerName;
        SecondsRemaining = secondsRemaining;
    }
}

/// <summary>
/// Event arguments for when a timer expires.
/// </summary>
public class TimerExpiredEventArgs : EventArgs
{
    public string PlayerName { get; }
    public BettingActionType DefaultAction { get; }

    public TimerExpiredEventArgs(string playerName, BettingActionType defaultAction)
    {
        PlayerName = playerName;
        DefaultAction = defaultAction;
    }
}

/// <summary>
/// Event arguments for when a player uses their time bank.
/// </summary>
public class TimeBankUsedEventArgs : EventArgs
{
    public string PlayerName { get; }
    public int SecondsAdded { get; }
    public int TimeBankRemaining { get; }

    public TimeBankUsedEventArgs(string playerName, int secondsAdded, int timeBankRemaining)
    {
        PlayerName = playerName;
        SecondsAdded = secondsAdded;
        TimeBankRemaining = timeBankRemaining;
    }
}
