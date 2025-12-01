using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Api.Features.Chat;

/// <summary>
/// Service for managing chat messages at poker tables.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Sends a chat message to a table.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="senderName">The name of the sender.</param>
    /// <param name="content">The message content.</param>
    /// <returns>The chat message if successful, or null with an error message if validation failed.</returns>
    Task<(ChatMessageDto? Message, string? Error)> SendMessageAsync(Guid tableId, string senderName, string content);

    /// <summary>
    /// Gets the chat history for a table.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="maxMessages">The maximum number of messages to return.</param>
    /// <returns>A list of chat messages.</returns>
    Task<IReadOnlyList<ChatMessageDto>> GetChatHistoryAsync(Guid tableId, int maxMessages = 50);

    /// <summary>
    /// Creates a system announcement message.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="content">The announcement content.</param>
    /// <returns>The created announcement message.</returns>
    Task<ChatMessageDto> CreateSystemAnnouncementAsync(Guid tableId, string content);

    /// <summary>
    /// Mutes a player for another player.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="playerName">The player doing the muting.</param>
    /// <param name="playerToMute">The player to mute.</param>
    /// <returns>True if successful, false with an error message if failed.</returns>
    Task<(bool Success, string? Error)> MutePlayerAsync(Guid tableId, string playerName, string playerToMute);

    /// <summary>
    /// Unmutes a player for another player.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="playerName">The player doing the unmuting.</param>
    /// <param name="playerToUnmute">The player to unmute.</param>
    /// <returns>True if successful, false with an error message if failed.</returns>
    Task<(bool Success, string? Error)> UnmutePlayerAsync(Guid tableId, string playerName, string playerToUnmute);

    /// <summary>
    /// Gets the list of players muted by a specific player at a table.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="playerName">The player whose mute list to get.</param>
    /// <returns>A list of muted player names.</returns>
    Task<IReadOnlyList<string>> GetMutedPlayersAsync(Guid tableId, string playerName);

    /// <summary>
    /// Checks if a player has muted another player.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="playerName">The player who may have muted someone.</param>
    /// <param name="mutedPlayerName">The potentially muted player.</param>
    /// <returns>True if the player is muted.</returns>
    Task<bool> IsPlayerMutedAsync(Guid tableId, string playerName, string mutedPlayerName);

    /// <summary>
    /// Enables or disables chat for an entire table.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <param name="enabled">Whether chat should be enabled.</param>
    /// <param name="changedByPlayerName">The player who changed the setting (optional, null for system).</param>
    /// <returns>True if successful, false with an error message if failed.</returns>
    Task<(bool Success, string? Error)> SetTableChatEnabledAsync(Guid tableId, bool enabled, string? changedByPlayerName = null);

    /// <summary>
    /// Checks if chat is enabled for a table.
    /// </summary>
    /// <param name="tableId">The table identifier.</param>
    /// <returns>True if chat is enabled.</returns>
    Task<bool> IsTableChatEnabledAsync(Guid tableId);
}
