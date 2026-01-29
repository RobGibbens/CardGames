using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Manages action timers for player turns and broadcasts timer updates via SignalR.
/// </summary>
public sealed class ActionTimerService : IActionTimerService, IDisposable
{
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActionTimerService> _logger;

    private readonly ConcurrentDictionary<Guid, GameTimerState> _activeTimers = new();
    private bool _disposed;

    private const string GameGroupPrefix = "game:";

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionTimerService"/> class.
    /// </summary>
    public ActionTimerService(
        IHubContext<GameHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<ActionTimerService> logger)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public void StartTimer(Guid gameId, int playerSeatIndex, int durationSeconds = IActionTimerService.DefaultTimerDurationSeconds, Func<Guid, int, Task>? onExpired = null)
    {
        // Stop any existing timer for this game
        StopTimer(gameId);

        var state = new ActionTimerState
        {
            GameId = gameId,
            PlayerSeatIndex = playerSeatIndex,
            DurationSeconds = durationSeconds,
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        var timerState = new GameTimerState
        {
            State = state,
            OnExpired = onExpired
        };

        // Create the timer that ticks every second
        var timer = new Timer(
            callback: async _ => await OnTimerTickAsync(gameId),
            state: null,
            dueTime: TimeSpan.FromSeconds(1),
            period: TimeSpan.FromSeconds(1));

        timerState.Timer = timer;

        _activeTimers[gameId] = timerState;

        _logger.LogInformation(
            "Started action timer for game {GameId}, player seat {SeatIndex}, duration {Duration}s",
            gameId, playerSeatIndex, durationSeconds);

        // Broadcast initial timer state
        _ = BroadcastTimerStateAsync(gameId, state);
    }

    /// <inheritdoc />
    public void StartChipCheckPauseTimer(Guid gameId, int durationSeconds = IActionTimerService.DefaultChipCheckPauseDurationSeconds, Func<Guid, Task>? onExpired = null)
    {
        // Stop any existing timer for this game
        StopTimer(gameId);

        var state = new ActionTimerState
        {
            GameId = gameId,
            PlayerSeatIndex = -1, // No specific player for chip check pause
            DurationSeconds = durationSeconds,
            StartedAtUtc = DateTimeOffset.UtcNow,
            TimerType = ActionTimerType.ChipCheckPause
        };

        var timerState = new GameTimerState
        {
            State = state,
            OnChipCheckExpired = onExpired
        };

        // Create the timer that ticks every second
        var timer = new Timer(
            callback: async _ => await OnTimerTickAsync(gameId),
            state: null,
            dueTime: TimeSpan.FromSeconds(1),
            period: TimeSpan.FromSeconds(1));

        timerState.Timer = timer;

        _activeTimers[gameId] = timerState;

        _logger.LogInformation(
            "Started chip check pause timer for game {GameId}, duration {Duration}s",
            gameId, durationSeconds);

        // Broadcast initial timer state
        _ = BroadcastTimerStateAsync(gameId, state);
    }

    /// <inheritdoc />
    public void StopTimer(Guid gameId)
    {
        if (_activeTimers.TryRemove(gameId, out var timerState))
        {
            timerState.Timer?.Dispose();
            _logger.LogInformation("Stopped action timer for game {GameId}", gameId);

            // Broadcast that timer is stopped
            _ = BroadcastTimerStoppedAsync(gameId);
        }
    }

    /// <inheritdoc />
    public ActionTimerState? GetTimerState(Guid gameId)
    {
        return _activeTimers.TryGetValue(gameId, out var timerState) ? timerState.State : null;
    }

    /// <inheritdoc />
    public bool IsTimerActive(Guid gameId)
    {
        return _activeTimers.ContainsKey(gameId);
    }

    private async Task OnTimerTickAsync(Guid gameId)
    {
        if (_disposed)
        {
            return;
        }

        if (!_activeTimers.TryGetValue(gameId, out var timerState))
        {
            return;
        }

        var state = timerState.State;
        var secondsRemaining = state.SecondsRemaining;

        // Broadcast timer update
        await BroadcastTimerStateAsync(gameId, state);

        // Check if timer has expired
        if (secondsRemaining <= 0)
        {
            _logger.LogInformation(
                "Action timer expired for game {GameId}, player seat {SeatIndex}, type {TimerType}",
                gameId, state.PlayerSeatIndex, state.TimerType);

            // Stop the timer
            StopTimer(gameId);

            // Invoke appropriate expiration callback based on timer type
            if (state.TimerType == ActionTimerType.ChipCheckPause && timerState.OnChipCheckExpired is not null)
            {
                try
                {
                    await timerState.OnChipCheckExpired(gameId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in chip check timer expiration callback for game {GameId}", gameId);
                }
            }
            else if (timerState.OnExpired is not null)
            {
                try
                {
                    await timerState.OnExpired(gameId, state.PlayerSeatIndex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in timer expiration callback for game {GameId}", gameId);
                }
            }
        }
    }

    private async Task BroadcastTimerStateAsync(Guid gameId, ActionTimerState state)
    {
        try
        {
            var groupName = GetGroupName(gameId);
            var dto = new ActionTimerStateDto
            {
                SecondsRemaining = state.SecondsRemaining,
                DurationSeconds = state.DurationSeconds,
                StartedAtUtc = state.StartedAtUtc,
                PlayerSeatIndex = state.PlayerSeatIndex,
                IsActive = true
            };

            await _hubContext.Clients.Group(groupName)
                .SendAsync("ActionTimerUpdated", dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting timer state for game {GameId}", gameId);
        }
    }

    private async Task BroadcastTimerStoppedAsync(Guid gameId)
    {
        try
        {
            var groupName = GetGroupName(gameId);
            var dto = new ActionTimerStateDto
            {
                SecondsRemaining = 0,
                DurationSeconds = 0,
                StartedAtUtc = DateTimeOffset.UtcNow,
                PlayerSeatIndex = -1,
                IsActive = false
            };

            await _hubContext.Clients.Group(groupName)
                .SendAsync("ActionTimerUpdated", dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting timer stopped for game {GameId}", gameId);
        }
    }

    private static string GetGroupName(Guid gameId) => $"{GameGroupPrefix}{gameId}";

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var kvp in _activeTimers)
        {
            kvp.Value.Timer?.Dispose();
        }

        _activeTimers.Clear();
    }

    private sealed class GameTimerState
    {
        public required ActionTimerState State { get; init; }
        public Timer? Timer { get; set; }
        public Func<Guid, int, Task>? OnExpired { get; init; }
        public Func<Guid, Task>? OnChipCheckExpired { get; init; }
    }
}
