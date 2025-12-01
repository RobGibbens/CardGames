using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.Events;

/// <summary>
/// Event raised when a chat message is sent to a table.
/// </summary>
public record ChatMessageSentEvent(
    Guid TableId,
    DateTime Timestamp,
    ChatMessageDto Message) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a system announcement is broadcast to a table.
/// </summary>
public record SystemAnnouncementEvent(
    Guid TableId,
    DateTime Timestamp,
    string Content,
    SystemAnnouncementType AnnouncementType) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a player mutes another player.
/// </summary>
public record PlayerMutedEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    string MutedPlayerName) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a player unmutes another player.
/// </summary>
public record PlayerUnmutedEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    string UnmutedPlayerName) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when table-wide chat is enabled or disabled.
/// </summary>
public record TableChatStatusChangedEvent(
    Guid TableId,
    DateTime Timestamp,
    bool IsChatEnabled,
    string? ChangedByPlayerName) : GameEvent(TableId, Timestamp);

/// <summary>
/// Event raised when a chat message is rejected due to validation failure.
/// </summary>
public record ChatMessageRejectedEvent(
    Guid TableId,
    DateTime Timestamp,
    string PlayerName,
    string Reason) : GameEvent(TableId, Timestamp);

/// <summary>
/// Specifies the type of system announcement.
/// </summary>
public enum SystemAnnouncementType
{
    /// <summary>A player joined the table.</summary>
    PlayerJoined,

    /// <summary>A player left the table.</summary>
    PlayerLeft,

    /// <summary>A player won the pot.</summary>
    PotWon,

    /// <summary>A new hand is starting.</summary>
    NewHand,

    /// <summary>Game is starting.</summary>
    GameStarted,

    /// <summary>Game has ended.</summary>
    GameEnded,

    /// <summary>A player timed out.</summary>
    PlayerTimeout,

    /// <summary>A player went all-in.</summary>
    AllIn,

    /// <summary>General information message.</summary>
    Info
}
