using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.Events;

namespace CardGames.Poker.Web.Services.StateManagement;

/// <summary>
/// Facade service that coordinates all state management components.
/// Provides a unified API for game state synchronization.
/// </summary>
public interface IStateSyncCoordinator : IAsyncDisposable
{
    /// <summary>
    /// Gets the state manager.
    /// </summary>
    IGameStateManager StateManager { get; }

    /// <summary>
    /// Gets the event replay service.
    /// </summary>
    IEventReplayService EventReplay { get; }

    /// <summary>
    /// Gets the state validation service.
    /// </summary>
    IStateValidationService Validation { get; }

    /// <summary>
    /// Gets the concurrent action handler.
    /// </summary>
    IConcurrentActionHandler ActionHandler { get; }

    /// <summary>
    /// Gets whether the coordinator is connected to a table.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets the current table ID.
    /// </summary>
    Guid? CurrentTableId { get; }

    /// <summary>
    /// Event raised when connection state changes.
    /// </summary>
    event Action<bool>? OnConnectionChanged;

    /// <summary>
    /// Connects to a table and starts state synchronization.
    /// </summary>
    /// <param name="tableId">The table to connect to.</param>
    /// <param name="playerName">The player's name.</param>
    Task ConnectAsync(Guid tableId, string playerName);

    /// <summary>
    /// Disconnects from the current table.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Performs a betting action with optimistic update support.
    /// </summary>
    /// <param name="actionType">The type of action.</param>
    /// <param name="amount">The action amount.</param>
    /// <returns>The result of the action attempt.</returns>
    Task<ActionAttemptResult> PerformActionAsync(BettingActionType actionType, int amount = 0);

    /// <summary>
    /// Requests a full state resynchronization.
    /// </summary>
    Task ResyncStateAsync();
}

/// <summary>
/// Result of an action attempt.
/// </summary>
public record ActionAttemptResult(
    bool Success,
    Guid? ActionId = null,
    string? ErrorMessage = null);

/// <summary>
/// Implementation of the state sync coordinator.
/// </summary>
public class StateSyncCoordinator : IStateSyncCoordinator
{
    private readonly ILogger<StateSyncCoordinator> _logger;
    private readonly GameHubService _hubService;
    private readonly IGameStateManager _stateManager;
    private readonly IEventReplayService _eventReplay;
    private readonly IStateValidationService _validation;
    private readonly IConcurrentActionHandler _actionHandler;

    private string? _playerName;
    private bool _disposed;

    public IGameStateManager StateManager => _stateManager;
    public IEventReplayService EventReplay => _eventReplay;
    public IStateValidationService Validation => _validation;
    public IConcurrentActionHandler ActionHandler => _actionHandler;

    public bool IsConnected { get; private set; }
    public Guid? CurrentTableId { get; private set; }

    public event Action<bool>? OnConnectionChanged;

    public StateSyncCoordinator(
        ILogger<StateSyncCoordinator> logger,
        GameHubService hubService,
        IGameStateManager stateManager,
        IEventReplayService eventReplay,
        IStateValidationService validation,
        IConcurrentActionHandler actionHandler)
    {
        _logger = logger;
        _hubService = hubService;
        _stateManager = stateManager;
        _eventReplay = eventReplay;
        _validation = validation;
        _actionHandler = actionHandler;

        // Subscribe to hub events
        SubscribeToHubEvents();
    }

    public async Task ConnectAsync(Guid tableId, string playerName)
    {
        if (IsConnected && CurrentTableId == tableId)
        {
            _logger.LogDebug("Already connected to table {TableId}", tableId);
            return;
        }

        // Disconnect from previous table if any
        if (IsConnected)
        {
            await DisconnectAsync();
        }

        _playerName = playerName;
        CurrentTableId = tableId;

        try
        {
            // Join the table via SignalR
            await _hubService.JoinTableAsync(tableId.ToString(), playerName);

            // Request initial state
            await _hubService.RequestTableStateAsync(tableId.ToString());

            // Start periodic validation
            _validation.StartPeriodicValidation(tableId, TimeSpan.FromSeconds(30));

            IsConnected = true;
            OnConnectionChanged?.Invoke(true);

            _logger.LogInformation(
                "Connected to table {TableId} as {PlayerName}",
                tableId, playerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to table {TableId}", tableId);
            await DisconnectAsync();
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected) return;

        try
        {
            if (CurrentTableId.HasValue)
            {
                await _hubService.LeaveTableAsync(CurrentTableId.Value.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving table");
        }
        finally
        {
            _validation.StopPeriodicValidation();
            _actionHandler.ClearPendingActions();
            _stateManager.Reset();

            if (CurrentTableId.HasValue)
            {
                _eventReplay.ClearEvents(CurrentTableId.Value);
            }

            CurrentTableId = null;
            _playerName = null;
            IsConnected = false;

            OnConnectionChanged?.Invoke(false);

            _logger.LogInformation("Disconnected from table");
        }
    }

    public async Task<ActionAttemptResult> PerformActionAsync(BettingActionType actionType, int amount = 0)
    {
        if (!IsConnected || CurrentTableId is null || _playerName is null)
        {
            return new ActionAttemptResult(false, null, "Not connected to a table");
        }

        // Create the pending action
        var pendingAction = new PendingAction(
            Guid.NewGuid(),
            _playerName,
            actionType,
            amount,
            DateTime.UtcNow,
            _stateManager.StateVersion);

        // Try to queue the action
        if (!_actionHandler.TryQueueAction(pendingAction))
        {
            return new ActionAttemptResult(false, null, "Action could not be queued");
        }

        // Apply optimistic update
        var optimisticUpdate = new OptimisticUpdate(
            OptimisticUpdateType.PlayerAction,
            _playerName,
            actionType,
            amount);

        var updateId = _stateManager.ApplyOptimisticUpdate(optimisticUpdate);

        // Send the action to the server
        try
        {
            var tableId = CurrentTableId.Value.ToString();
            switch (actionType)
            {
                case BettingActionType.Fold:
                    await _hubService.FoldAsync(tableId);
                    break;
                case BettingActionType.Check:
                    await _hubService.CheckAsync(tableId);
                    break;
                case BettingActionType.Call:
                    await _hubService.CallAsync(tableId, amount);
                    break;
                case BettingActionType.Bet:
                    await _hubService.BetAsync(tableId, amount);
                    break;
                case BettingActionType.Raise:
                    await _hubService.RaiseAsync(tableId, amount);
                    break;
                case BettingActionType.AllIn:
                    await _hubService.AllInAsync(tableId, amount);
                    break;
                default:
                    _actionHandler.CancelAction(pendingAction.Id);
                    _stateManager.RejectOptimisticUpdate(updateId, "Unknown action type");
                    return new ActionAttemptResult(false, null, "Unknown action type");
            }

            _logger.LogDebug(
                "Action {ActionType} sent to server for player {PlayerName}",
                actionType, _playerName);

            return new ActionAttemptResult(true, pendingAction.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send action to server");
            _actionHandler.RejectAction(pendingAction.Id, ex.Message);
            _stateManager.RejectOptimisticUpdate(updateId, ex.Message);
            return new ActionAttemptResult(false, null, ex.Message);
        }
    }

    public async Task ResyncStateAsync()
    {
        if (!IsConnected || CurrentTableId is null)
        {
            _logger.LogWarning("Cannot resync - not connected");
            return;
        }

        _logger.LogInformation("Requesting full state resync for table {TableId}", CurrentTableId);

        // Clear pending actions
        _actionHandler.ClearPendingActions();

        // Request fresh state
        await _hubService.RequestTableStateAsync(CurrentTableId.Value.ToString());
    }

    private void SubscribeToHubEvents()
    {
        // Table state sync
        _hubService.OnTableStateSync += HandleTableStateSync;

        // Game events
        _hubService.OnPlayerAction += HandlePlayerAction;
        _hubService.OnPlayerTurn += HandlePlayerTurn;
        _hubService.OnHandStarted += HandleHandStarted;
        _hubService.OnCommunityCardsDealt += HandleCommunityCardsDealt;
        _hubService.OnPlayerConnected += HandlePlayerConnected;
        _hubService.OnPlayerDisconnected += HandlePlayerDisconnected;
        _hubService.OnPlayerReconnected += HandlePlayerReconnected;

        // Action rejection
        _hubService.OnActionRejected += HandleActionRejected;

        // Connection events
        _hubService.OnConnectionChanged += HandleConnectionChanged;

        // Validation events
        _validation.OnReconciliationNeeded += HandleReconciliationNeeded;
    }

    private void HandleTableStateSync(TableStateSyncEvent evt)
    {
        if (evt.TableId != CurrentTableId) return;

        _stateManager.Initialize(evt.TableId, evt.Snapshot);

        if (evt.Snapshot.CurrentPlayerName is not null)
        {
            _actionHandler.SetCurrentPlayer(evt.Snapshot.CurrentPlayerName);
        }

        _logger.LogDebug("Received table state sync for table {TableId}", evt.TableId);
    }

    private void HandlePlayerAction(BettingActionEvent evt)
    {
        if (evt.GameId != CurrentTableId) return;

        // Record the event
        _eventReplay.RecordEvent(evt);

        // Check if this confirms our pending action
        if (_actionHandler.CurrentPendingAction is not null &&
            _actionHandler.CurrentPendingAction.PlayerName == evt.Action.PlayerName)
        {
            _actionHandler.ConfirmAction(_actionHandler.CurrentPendingAction.Id);
        }

        // Apply to state (server is authoritative)
        _stateManager.ApplyServerEvent(evt);
    }

    private void HandlePlayerTurn(PlayerTurnEvent evt)
    {
        if (evt.GameId != CurrentTableId) return;

        _eventReplay.RecordEvent(evt);
        _actionHandler.SetCurrentPlayer(evt.PlayerName);
        _stateManager.ApplyServerEvent(evt);
    }

    private void HandleHandStarted(HandStartedEvent evt)
    {
        if (evt.GameId != CurrentTableId) return;

        // Clear pending actions for new hand
        _actionHandler.ClearPendingActions();

        _eventReplay.RecordEvent(evt);
        _stateManager.ApplyServerEvent(evt);
    }

    private void HandleCommunityCardsDealt(CommunityCardsDealtEvent evt)
    {
        if (evt.GameId != CurrentTableId) return;

        _eventReplay.RecordEvent(evt);
        _stateManager.ApplyServerEvent(evt);
    }

    private void HandlePlayerConnected(PlayerConnectedEvent evt)
    {
        if (evt.TableId != CurrentTableId) return;

        _eventReplay.RecordEvent(evt);
        _stateManager.ApplyServerEvent(evt);
    }

    private void HandlePlayerDisconnected(PlayerDisconnectedEvent evt)
    {
        if (evt.TableId != CurrentTableId) return;

        _eventReplay.RecordEvent(evt);
        _stateManager.ApplyServerEvent(evt);
    }

    private void HandlePlayerReconnected(PlayerReconnectedEvent evt)
    {
        if (evt.TableId != CurrentTableId) return;

        _eventReplay.RecordEvent(evt);
        _stateManager.ApplyServerEvent(evt);

        // Request full state sync on reconnection
        if (evt.PlayerName == _playerName)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Reconnected - requesting state sync");
                    await ResyncStateAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to resync state after reconnection");
                }
            });
        }
    }

    private void HandleActionRejected(string reason)
    {
        if (_actionHandler.CurrentPendingAction is not null)
        {
            var actionId = _actionHandler.CurrentPendingAction.Id;
            _actionHandler.RejectAction(actionId, reason);

            _logger.LogWarning("Action rejected by server: {Reason}", reason);
        }
    }

    private void HandleConnectionChanged(string state)
    {
        if (state == "Disconnected")
        {
            IsConnected = false;
            OnConnectionChanged?.Invoke(false);
        }
        else if (state == "Connected")
        {
            IsConnected = true;
            OnConnectionChanged?.Invoke(true);
        }
    }

    private void HandleReconciliationNeeded(StateValidationResult result)
    {
        _logger.LogWarning(
            "State reconciliation needed: {ConflictCount} conflicts detected",
            result.Conflicts.Count);

        // Trigger resync
        _ = Task.Run(async () =>
        {
            try
            {
                await ResyncStateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resync state after conflicts detected");
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await DisconnectAsync();

        if (_validation is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
    }
}
