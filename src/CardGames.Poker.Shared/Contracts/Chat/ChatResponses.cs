using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Shared.Contracts.Chat;

/// <summary>
/// Response for sending a chat message.
/// </summary>
public record SendChatMessageResponse(
    bool Success,
    ChatMessageDto? Message = null,
    string? Error = null);

/// <summary>
/// Response for muting a player.
/// </summary>
public record MutePlayerResponse(
    bool Success,
    string? Error = null);

/// <summary>
/// Response for unmuting a player.
/// </summary>
public record UnmutePlayerResponse(
    bool Success,
    string? Error = null);

/// <summary>
/// Response for setting table chat status.
/// </summary>
public record SetTableChatStatusResponse(
    bool Success,
    bool IsChatEnabled,
    string? Error = null);

/// <summary>
/// Response for getting chat history.
/// </summary>
public record GetChatHistoryResponse(
    bool Success,
    IReadOnlyList<ChatMessageDto>? Messages = null,
    string? Error = null);

/// <summary>
/// Response for getting muted players list.
/// </summary>
public record GetMutedPlayersResponse(
    bool Success,
    IReadOnlyList<string>? MutedPlayers = null,
    string? Error = null);
