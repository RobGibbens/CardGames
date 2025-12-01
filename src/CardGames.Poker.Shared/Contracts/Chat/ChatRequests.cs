namespace CardGames.Poker.Shared.Contracts.Chat;

/// <summary>
/// Request to send a chat message at a table.
/// </summary>
public record SendChatMessageRequest(
    Guid TableId,
    string Content);

/// <summary>
/// Request to mute or unmute a specific player.
/// </summary>
public record MutePlayerRequest(
    Guid TableId,
    string PlayerToMute);

/// <summary>
/// Request to unmute a specific player.
/// </summary>
public record UnmutePlayerRequest(
    Guid TableId,
    string PlayerToUnmute);

/// <summary>
/// Request to enable or disable table-wide chat.
/// </summary>
public record SetTableChatStatusRequest(
    Guid TableId,
    bool EnableChat);

/// <summary>
/// Request to get chat message history for a table.
/// </summary>
public record GetChatHistoryRequest(
    Guid TableId,
    int MaxMessages = 50);

/// <summary>
/// Request to get the list of muted players.
/// </summary>
public record GetMutedPlayersRequest(
    Guid TableId);
