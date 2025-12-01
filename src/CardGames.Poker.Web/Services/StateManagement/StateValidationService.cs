using CardGames.Poker.Shared.Events;

namespace CardGames.Poker.Web.Services.StateManagement;

/// <summary>
/// Service for validating client state against server state periodically.
/// </summary>
public interface IStateValidationService
{
    /// <summary>
    /// Gets whether validation is currently enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets the interval between validation checks.
    /// </summary>
    TimeSpan ValidationInterval { get; }

    /// <summary>
    /// Gets the timestamp of the last validation.
    /// </summary>
    DateTime? LastValidationTime { get; }

    /// <summary>
    /// Gets the result of the last validation.
    /// </summary>
    StateValidationResult? LastValidationResult { get; }

    /// <summary>
    /// Event raised when validation completes.
    /// </summary>
    event Action<StateValidationResult>? OnValidationCompleted;

    /// <summary>
    /// Event raised when validation fails and reconciliation is needed.
    /// </summary>
    event Action<StateValidationResult>? OnReconciliationNeeded;

    /// <summary>
    /// Starts periodic validation.
    /// </summary>
    /// <param name="tableId">The table to validate.</param>
    /// <param name="interval">The interval between checks.</param>
    void StartPeriodicValidation(Guid tableId, TimeSpan interval);

    /// <summary>
    /// Stops periodic validation.
    /// </summary>
    void StopPeriodicValidation();

    /// <summary>
    /// Triggers an immediate validation check.
    /// </summary>
    /// <returns>The validation result.</returns>
    Task<StateValidationResult> ValidateNowAsync();

    /// <summary>
    /// Sets the threshold for triggering automatic reconciliation.
    /// </summary>
    /// <param name="maxConflicts">Maximum allowed conflicts before reconciliation.</param>
    void SetReconciliationThreshold(int maxConflicts);
}

/// <summary>
/// Implementation of state validation service.
/// </summary>
public class StateValidationService : IStateValidationService, IDisposable
{
    private readonly ILogger<StateValidationService> _logger;
    private readonly IGameStateManager _stateManager;
    private readonly GameHubService _hubService;
    private readonly object _lock = new();

    private Timer? _validationTimer;
    private Guid _tableId;
    private int _reconciliationThreshold = 3;
    private bool _disposed;

    public bool IsEnabled { get; private set; }
    public TimeSpan ValidationInterval { get; private set; } = TimeSpan.FromSeconds(30);
    public DateTime? LastValidationTime { get; private set; }
    public StateValidationResult? LastValidationResult { get; private set; }

    public event Action<StateValidationResult>? OnValidationCompleted;
    public event Action<StateValidationResult>? OnReconciliationNeeded;

    public StateValidationService(
        ILogger<StateValidationService> logger,
        IGameStateManager stateManager,
        GameHubService hubService)
    {
        _logger = logger;
        _stateManager = stateManager;
        _hubService = hubService;

        // Subscribe to table state sync events
        _hubService.OnTableStateSync += HandleTableStateSync;
    }

    public void StartPeriodicValidation(Guid tableId, TimeSpan interval)
    {
        lock (_lock)
        {
            StopPeriodicValidation();

            _tableId = tableId;
            ValidationInterval = interval;
            IsEnabled = true;

            _validationTimer = new Timer(
                _ => OnValidationTimerElapsed(),
                null,
                interval,
                interval);

            _logger.LogInformation(
                "Started periodic validation for table {TableId} with interval {Interval}",
                tableId, interval);
        }
    }

    public void StopPeriodicValidation()
    {
        lock (_lock)
        {
            _validationTimer?.Dispose();
            _validationTimer = null;
            IsEnabled = false;

            _logger.LogDebug("Stopped periodic validation");
        }
    }

    public async Task<StateValidationResult> ValidateNowAsync()
    {
        if (_tableId == Guid.Empty)
        {
            _logger.LogWarning("Cannot validate - no table ID set");
            return new StateValidationResult(false, [], 0, 0);
        }

        // Wait for response (with timeout)
        var tcs = new TaskCompletionSource<StateValidationResult>();
        var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        void Handler(TableStateSyncEvent evt)
        {
            if (evt.TableId == _tableId)
            {
                var result = _stateManager.ValidateAgainstServer(evt.Snapshot);
                tcs.TrySetResult(result);
            }
        }

        _hubService.OnTableStateSync += Handler;

        try
        {
            // Request current state from server after subscribing to ensure we don't miss the response
            await _hubService.RequestTableStateAsync(_tableId.ToString());

            var completedTask = await Task.WhenAny(
                tcs.Task,
                Task.Delay(Timeout.Infinite, timeoutCts.Token));

            if (!tcs.Task.IsCompleted)
            {
                _logger.LogWarning("Validation request timed out for table {TableId}", _tableId);
                return new StateValidationResult(false, [], _stateManager.StateVersion, 0);
            }

            var result = await tcs.Task;
            ProcessValidationResult(result);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Validation request was cancelled for table {TableId}", _tableId);
            return new StateValidationResult(false, [], _stateManager.StateVersion, 0);
        }
        finally
        {
            _hubService.OnTableStateSync -= Handler;
            timeoutCts.Dispose();
        }
    }

    public void SetReconciliationThreshold(int maxConflicts)
    {
        _reconciliationThreshold = Math.Max(1, maxConflicts);
        _logger.LogDebug("Set reconciliation threshold to {Threshold}", _reconciliationThreshold);
    }

    private void HandleTableStateSync(TableStateSyncEvent evt)
    {
        if (!IsEnabled || evt.TableId != _tableId) return;

        var result = _stateManager.ValidateAgainstServer(evt.Snapshot);
        ProcessValidationResult(result);
    }

    private void ProcessValidationResult(StateValidationResult result)
    {
        lock (_lock)
        {
            LastValidationTime = DateTime.UtcNow;
            LastValidationResult = result;

            _logger.LogDebug(
                "Validation completed: Valid={IsValid}, Conflicts={ConflictCount}",
                result.IsValid, result.Conflicts.Count);

            OnValidationCompleted?.Invoke(result);

            if (!result.IsValid && result.Conflicts.Count >= _reconciliationThreshold)
            {
                _logger.LogWarning(
                    "Reconciliation needed: {ConflictCount} conflicts exceed threshold {Threshold}",
                    result.Conflicts.Count, _reconciliationThreshold);

                OnReconciliationNeeded?.Invoke(result);
            }
        }
    }

    private void OnValidationTimerElapsed()
    {
        if (!IsEnabled) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await ValidateNowAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic validation");
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;

        _hubService.OnTableStateSync -= HandleTableStateSync;
        _validationTimer?.Dispose();
        _disposed = true;
    }
}
