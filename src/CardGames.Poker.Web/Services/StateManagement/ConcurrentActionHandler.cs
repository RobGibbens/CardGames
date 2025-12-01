using System.Collections.Concurrent;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Web.Services.StateManagement;

/// <summary>
/// Service for handling concurrent actions and preventing race conditions.
/// </summary>
public interface IConcurrentActionHandler
{
    /// <summary>
    /// Gets whether an action is currently pending.
    /// </summary>
    bool HasPendingAction { get; }

    /// <summary>
    /// Gets the current pending action if any.
    /// </summary>
    PendingAction? CurrentPendingAction { get; }

    /// <summary>
    /// Event raised when an action is queued.
    /// </summary>
    event Action<PendingAction>? OnActionQueued;

    /// <summary>
    /// Event raised when an action is processed.
    /// </summary>
    event Action<ActionResult>? OnActionProcessed;

    /// <summary>
    /// Event raised when an action is rejected due to race condition.
    /// </summary>
    event Action<PendingAction, string>? OnActionRejected;

    /// <summary>
    /// Attempts to queue an action for processing.
    /// </summary>
    /// <param name="action">The action to queue.</param>
    /// <returns>True if the action was queued, false if rejected.</returns>
    bool TryQueueAction(PendingAction action);

    /// <summary>
    /// Confirms that the current action was processed successfully.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    void ConfirmAction(Guid actionId);

    /// <summary>
    /// Rejects the current action.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    /// <param name="reason">The rejection reason.</param>
    void RejectAction(Guid actionId, string reason);

    /// <summary>
    /// Cancels a pending action.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    void CancelAction(Guid actionId);

    /// <summary>
    /// Clears all pending actions.
    /// </summary>
    void ClearPendingActions();

    /// <summary>
    /// Checks if an action can be performed by the given player.
    /// </summary>
    /// <param name="playerName">The player attempting the action.</param>
    /// <returns>True if the player can perform an action.</returns>
    bool CanPlayerAct(string playerName);

    /// <summary>
    /// Sets the current player who is allowed to act.
    /// </summary>
    /// <param name="playerName">The player name.</param>
    void SetCurrentPlayer(string? playerName);
}

/// <summary>
/// Represents a pending action waiting to be processed.
/// </summary>
public record PendingAction(
    Guid Id,
    string PlayerName,
    BettingActionType ActionType,
    int Amount,
    DateTime QueuedAt,
    long StateVersionAtQueue);

/// <summary>
/// Result of an action processing.
/// </summary>
public record ActionResult(
    Guid ActionId,
    bool Success,
    string? ErrorMessage = null,
    long? NewStateVersion = null);

/// <summary>
/// Implementation of concurrent action handler.
/// </summary>
public class ConcurrentActionHandler : IConcurrentActionHandler
{
    private readonly ILogger<ConcurrentActionHandler> _logger;
    private readonly IGameStateManager _stateManager;
    private readonly object _lock = new();
    private readonly ConcurrentQueue<PendingAction> _actionQueue = new();
    private readonly TimeSpan _actionTimeout = TimeSpan.FromSeconds(10);

    private PendingAction? _currentPendingAction;
    private string? _currentPlayerName;
    private DateTime? _actionStartedAt;

    public bool HasPendingAction => _currentPendingAction is not null;
    public PendingAction? CurrentPendingAction => _currentPendingAction;

    public event Action<PendingAction>? OnActionQueued;
    public event Action<ActionResult>? OnActionProcessed;
    public event Action<PendingAction, string>? OnActionRejected;

    public ConcurrentActionHandler(
        ILogger<ConcurrentActionHandler> logger,
        IGameStateManager stateManager)
    {
        _logger = logger;
        _stateManager = stateManager;
    }

    public bool TryQueueAction(PendingAction action)
    {
        lock (_lock)
        {
            // Check if action can be performed
            if (!CanPlayerAct(action.PlayerName))
            {
                _logger.LogWarning(
                    "Action rejected: Player {PlayerName} cannot act (current player: {CurrentPlayer})",
                    action.PlayerName, _currentPlayerName);

                OnActionRejected?.Invoke(action, "It is not your turn to act.");
                return false;
            }

            // Check for existing pending action
            if (_currentPendingAction is not null)
            {
                // Check if previous action has timed out
                if (_actionStartedAt.HasValue &&
                    DateTime.UtcNow - _actionStartedAt.Value > _actionTimeout)
                {
                    _logger.LogWarning(
                        "Previous action {ActionId} timed out, allowing new action",
                        _currentPendingAction.Id);

                    // Clear the timed out action
                    _currentPendingAction = null;
                    _actionStartedAt = null;
                }
                else
                {
                    // Queue the action for later processing
                    _actionQueue.Enqueue(action);

                    _logger.LogDebug(
                        "Action {ActionId} queued (pending action exists)",
                        action.Id);

                    return true;
                }
            }

            // Validate state version to detect stale actions
            if (action.StateVersionAtQueue < _stateManager.StateVersion)
            {
                _logger.LogWarning(
                    "Action rejected: State version mismatch (action: {ActionVersion}, current: {CurrentVersion})",
                    action.StateVersionAtQueue, _stateManager.StateVersion);

                OnActionRejected?.Invoke(action, "Action was based on outdated game state. Please try again.");
                return false;
            }

            // Set as current pending action
            _currentPendingAction = action;
            _actionStartedAt = DateTime.UtcNow;

            _logger.LogDebug(
                "Action {ActionId} ({ActionType}) queued for player {PlayerName}",
                action.Id, action.ActionType, action.PlayerName);

            OnActionQueued?.Invoke(action);
            return true;
        }
    }

    public void ConfirmAction(Guid actionId)
    {
        lock (_lock)
        {
            if (_currentPendingAction?.Id != actionId)
            {
                _logger.LogDebug(
                    "Confirm ignored: Action {ActionId} is not the current pending action",
                    actionId);
                return;
            }

            _logger.LogDebug(
                "Action {ActionId} confirmed",
                actionId);

            var result = new ActionResult(
                actionId,
                true,
                null,
                _stateManager.StateVersion);

            _currentPendingAction = null;
            _actionStartedAt = null;

            OnActionProcessed?.Invoke(result);

            // Process next queued action if any
            ProcessNextQueuedAction();
        }
    }

    public void RejectAction(Guid actionId, string reason)
    {
        lock (_lock)
        {
            if (_currentPendingAction?.Id != actionId)
            {
                _logger.LogDebug(
                    "Reject ignored: Action {ActionId} is not the current pending action",
                    actionId);
                return;
            }

            var action = _currentPendingAction;

            _logger.LogWarning(
                "Action {ActionId} rejected: {Reason}",
                actionId, reason);

            _currentPendingAction = null;
            _actionStartedAt = null;

            OnActionRejected?.Invoke(action, reason);
            OnActionProcessed?.Invoke(new ActionResult(actionId, false, reason));

            // Process next queued action if any
            ProcessNextQueuedAction();
        }
    }

    public void CancelAction(Guid actionId)
    {
        lock (_lock)
        {
            if (_currentPendingAction?.Id == actionId)
            {
                _logger.LogDebug("Action {ActionId} cancelled", actionId);

                _currentPendingAction = null;
                _actionStartedAt = null;

                // Process next queued action if any
                ProcessNextQueuedAction();
            }
        }
    }

    public void ClearPendingActions()
    {
        lock (_lock)
        {
            _currentPendingAction = null;
            _actionStartedAt = null;

            while (_actionQueue.TryDequeue(out _))
            {
                // Clear all queued actions
            }

            _logger.LogDebug("All pending actions cleared");
        }
    }

    public bool CanPlayerAct(string playerName)
    {
        lock (_lock)
        {
            // If no current player is set, allow any action
            if (_currentPlayerName is null)
            {
                return true;
            }

            return string.Equals(playerName, _currentPlayerName, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetCurrentPlayer(string? playerName)
    {
        lock (_lock)
        {
            var previousPlayer = _currentPlayerName;
            _currentPlayerName = playerName;

            if (previousPlayer != playerName)
            {
                _logger.LogDebug(
                    "Current player changed: {PreviousPlayer} -> {CurrentPlayer}",
                    previousPlayer, playerName);

                // Clear pending action from previous player
                if (_currentPendingAction is not null &&
                    _currentPendingAction.PlayerName != playerName)
                {
                    _logger.LogDebug(
                        "Clearing pending action {ActionId} due to player change",
                        _currentPendingAction.Id);

                    _currentPendingAction = null;
                    _actionStartedAt = null;
                }

                // Process next queued action for new player if any
                ProcessNextQueuedAction();
            }
        }
    }

    private void ProcessNextQueuedAction()
    {
        while (_actionQueue.TryDequeue(out var nextAction))
        {
            // Skip actions from wrong player
            if (!CanPlayerAct(nextAction.PlayerName))
            {
                _logger.LogDebug(
                    "Skipping queued action {ActionId}: wrong player",
                    nextAction.Id);
                continue;
            }

            // Skip stale actions
            if (nextAction.StateVersionAtQueue < _stateManager.StateVersion)
            {
                _logger.LogDebug(
                    "Skipping queued action {ActionId}: stale state version",
                    nextAction.Id);
                continue;
            }

            // Process this action
            _currentPendingAction = nextAction;
            _actionStartedAt = DateTime.UtcNow;

            _logger.LogDebug(
                "Processing queued action {ActionId}",
                nextAction.Id);

            OnActionQueued?.Invoke(nextAction);
            break;
        }
    }
}
