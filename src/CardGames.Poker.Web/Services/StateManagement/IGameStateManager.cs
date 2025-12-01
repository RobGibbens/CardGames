using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.Events;

namespace CardGames.Poker.Web.Services.StateManagement;

/// <summary>
/// Manages local game state for a Blazor client, providing synchronization
/// with the server state, optimistic updates, and state reconciliation.
/// </summary>
public interface IGameStateManager
{
    /// <summary>
    /// Gets the current table state.
    /// </summary>
    GameStateSnapshot? CurrentState { get; }

    /// <summary>
    /// Gets whether there are pending optimistic updates.
    /// </summary>
    bool HasPendingUpdates { get; }

    /// <summary>
    /// Gets the current state version for synchronization.
    /// </summary>
    long StateVersion { get; }

    /// <summary>
    /// Event raised when the state changes.
    /// </summary>
    event Action<GameStateSnapshot>? OnStateChanged;

    /// <summary>
    /// Event raised when a state conflict is detected.
    /// </summary>
    event Action<StateConflict>? OnStateConflict;

    /// <summary>
    /// Event raised when optimistic update is confirmed or rejected.
    /// </summary>
    event Action<OptimisticUpdateResult>? OnOptimisticUpdateResult;

    /// <summary>
    /// Initializes the state manager with a full state from the server.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="snapshot">The initial state snapshot from the server.</param>
    void Initialize(Guid tableId, TableStateSnapshot snapshot);

    /// <summary>
    /// Applies a server event to update the state.
    /// </summary>
    /// <param name="gameEvent">The game event from the server.</param>
    void ApplyServerEvent(GameEvent gameEvent);

    /// <summary>
    /// Applies an optimistic update before server confirmation.
    /// </summary>
    /// <param name="update">The optimistic update to apply.</param>
    /// <returns>The pending update identifier.</returns>
    Guid ApplyOptimisticUpdate(OptimisticUpdate update);

    /// <summary>
    /// Confirms an optimistic update when server acknowledges it.
    /// </summary>
    /// <param name="updateId">The pending update identifier.</param>
    void ConfirmOptimisticUpdate(Guid updateId);

    /// <summary>
    /// Rejects an optimistic update and rolls back the state.
    /// </summary>
    /// <param name="updateId">The pending update identifier.</param>
    /// <param name="reason">The reason for rejection.</param>
    void RejectOptimisticUpdate(Guid updateId, string reason);

    /// <summary>
    /// Creates a snapshot of the current state.
    /// </summary>
    /// <returns>A snapshot that can be used for state restoration.</returns>
    StateSnapshot CreateSnapshot();

    /// <summary>
    /// Restores state from a snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot to restore from.</param>
    void RestoreSnapshot(StateSnapshot snapshot);

    /// <summary>
    /// Validates the current state against server state.
    /// </summary>
    /// <param name="serverState">The server state to validate against.</param>
    /// <returns>The validation result with any discrepancies.</returns>
    StateValidationResult ValidateAgainstServer(TableStateSnapshot serverState);

    /// <summary>
    /// Reconciles local state with server state.
    /// </summary>
    /// <param name="serverState">The authoritative server state.</param>
    void ReconcileWithServer(TableStateSnapshot serverState);

    /// <summary>
    /// Replays events to rebuild state from a known point.
    /// </summary>
    /// <param name="events">The events to replay in order.</param>
    void ReplayEvents(IEnumerable<GameEvent> events);

    /// <summary>
    /// Clears all state and resets the manager.
    /// </summary>
    void Reset();
}

/// <summary>
/// Represents the current game state snapshot for the client.
/// </summary>
public record GameStateSnapshot(
    Guid TableId,
    string TableName,
    PokerVariant Variant,
    GameState State,
    IReadOnlyList<ClientPlayerState> Players,
    IReadOnlyList<CardDto>? CommunityCards,
    int DealerPosition,
    int CurrentPlayerPosition,
    string? CurrentPlayerName,
    int SmallBlind,
    int BigBlind,
    int Pot,
    int CurrentBet,
    string? CurrentStreet,
    int HandNumber,
    long Version,
    DateTime LastUpdated);

/// <summary>
/// Represents a player's state as seen by the client.
/// </summary>
public record ClientPlayerState(
    string Name,
    int Position,
    int ChipStack,
    int CurrentBet,
    bool HasFolded,
    bool IsAllIn,
    bool IsConnected,
    bool IsAway,
    IReadOnlyList<CardDto>? HoleCards,
    bool IsCurrentPlayer,
    bool IsDealer);

/// <summary>
/// Represents a state conflict between client and server.
/// </summary>
public record StateConflict(
    string PropertyName,
    object? ClientValue,
    object? ServerValue,
    string Description);

/// <summary>
/// Represents an optimistic update to be applied before server confirmation.
/// </summary>
public record OptimisticUpdate(
    OptimisticUpdateType Type,
    string PlayerName,
    BettingActionType? ActionType = null,
    int? Amount = null,
    IReadOnlyList<CardDto>? Cards = null);

/// <summary>
/// Types of optimistic updates.
/// </summary>
public enum OptimisticUpdateType
{
    PlayerAction,
    PlayerFolded,
    ChipStackChange,
    PlayerTurn,
    CommunityCardsDealt
}

/// <summary>
/// Result of an optimistic update confirmation or rejection.
/// </summary>
public record OptimisticUpdateResult(
    Guid UpdateId,
    bool IsConfirmed,
    string? RejectionReason = null);

/// <summary>
/// A snapshot of state for backup and restoration.
/// </summary>
public record StateSnapshot(
    Guid Id,
    DateTime CreatedAt,
    long Version,
    GameStateSnapshot State,
    IReadOnlyList<PendingOptimisticUpdate> PendingUpdates);

/// <summary>
/// Represents a pending optimistic update.
/// </summary>
public record PendingOptimisticUpdate(
    Guid Id,
    OptimisticUpdate Update,
    DateTime AppliedAt,
    GameStateSnapshot? PreUpdateState);

/// <summary>
/// Result of state validation against server.
/// </summary>
public record StateValidationResult(
    bool IsValid,
    IReadOnlyList<StateConflict> Conflicts,
    long ClientVersion,
    long ServerVersion);
