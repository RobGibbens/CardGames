using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.Events;

/// <summary>
/// Request for a player to perform a betting action.
/// </summary>
public record PlayerActionRequest(
    Guid TableId,
    string PlayerName,
    BettingActionType ActionType,
    int Amount = 0);

/// <summary>
/// Event raised when a player connects to a table.
/// </summary>
public record PlayerConnectedEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    string ConnectionId) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a player disconnects from a table.
/// </summary>
public record PlayerDisconnectedEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    bool WasUnexpected) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a player successfully reconnects to a table.
/// </summary>
public record PlayerReconnectedEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    TimeSpan DisconnectedDuration) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised to request a full table state synchronization.
/// </summary>
public record TableStateSyncRequest(
    Guid TableId,
    string PlayerName);

/// <summary>
/// Event containing full table state for synchronization.
/// </summary>
public record TableStateSyncEvent(
    Guid TableId,
    DateTime Timestamp,
    TableStateSnapshot Snapshot) : GameEvent(TableId, Timestamp);

/// <summary>
/// Snapshot of the current table state for synchronization.
/// </summary>
public record TableStateSnapshot(
    Guid TableId,
    string TableName,
    PokerVariant Variant,
    GameState State,
    IReadOnlyList<PlayerStateSnapshot> Players,
    IReadOnlyList<string>? CommunityCards,
    int DealerPosition,
    int CurrentPlayerPosition,
    string? CurrentPlayerName,
    int SmallBlind,
    int BigBlind,
    int Pot,
    int CurrentBet,
    string? CurrentStreet,
    int HandNumber,
    AvailableActionsSnapshot? AvailableActions);

/// <summary>
/// Snapshot of a player's state for synchronization.
/// </summary>
public record PlayerStateSnapshot(
    string Name,
    int Position,
    int ChipStack,
    int CurrentBet,
    bool HasFolded,
    bool IsAllIn,
    bool IsConnected,
    bool IsAway,
    IReadOnlyList<string>? VisibleCards);

/// <summary>
/// Snapshot of available actions for the current player.
/// </summary>
public record AvailableActionsSnapshot(
    string PlayerName,
    bool CanCheck,
    bool CanBet,
    bool CanCall,
    bool CanRaise,
    bool CanFold,
    bool CanAllIn,
    int MinBet,
    int MaxBet,
    int CallAmount,
    int MinRaise,
    int MaxRaise);

/// <summary>
/// Event raised when connection health (heartbeat) is received.
/// </summary>
public record HeartbeatEvent(
    string ConnectionId,
    DateTime Timestamp);

/// <summary>
/// Event raised when a player's connection is considered stale.
/// </summary>
public record ConnectionStaleEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    TimeSpan InactiveDuration) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when private information is sent to a specific player.
/// </summary>
public record PrivatePlayerDataEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    IReadOnlyList<string> HoleCards,
    string? HandDescription) : GameEvent(TableId, Timestamp);
