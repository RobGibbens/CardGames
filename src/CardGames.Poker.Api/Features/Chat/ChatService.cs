using System.Collections.Concurrent;
using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Api.Features.Chat;

/// <summary>
/// In-memory implementation of the chat service.
/// </summary>
public class ChatService : IChatService
{
    private readonly ILogger<ChatService> _logger;
    private readonly IChatMessageValidator _validator;

    // Store messages per table (limited history)
    private readonly ConcurrentDictionary<Guid, List<ChatMessageDto>> _tableMessages = new();
    private readonly int _maxHistoryPerTable = 100;

    // Store muted players: tableId -> (playerName -> set of muted player names)
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, HashSet<string>>> _mutedPlayers = new();

    // Store table chat enabled status
    private readonly ConcurrentDictionary<Guid, bool> _tableChatEnabled = new();

    public ChatService(ILogger<ChatService> logger, IChatMessageValidator validator)
    {
        _logger = logger;
        _validator = validator;
    }

    /// <inheritdoc />
    public Task<(ChatMessageDto? Message, string? Error)> SendMessageAsync(Guid tableId, string senderName, string content)
    {
        // Check if table chat is enabled
        if (!_tableChatEnabled.GetOrAdd(tableId, true))
        {
            return Task.FromResult<(ChatMessageDto?, string?)>((null, "Chat is disabled for this table."));
        }

        // Validate the message
        var (isValid, validationError) = _validator.Validate(senderName, content);
        if (!isValid)
        {
            _logger.LogWarning(
                "Chat message validation failed for player {PlayerName} at table {TableId}: {Error}",
                senderName, tableId, validationError);
            return Task.FromResult<(ChatMessageDto?, string?)>((null, validationError));
        }

        var message = new ChatMessageDto(
            MessageId: Guid.NewGuid(),
            TableId: tableId,
            SenderName: senderName,
            Content: content.Trim(),
            MessageType: ChatMessageType.Player,
            Timestamp: DateTime.UtcNow);

        AddMessageToHistory(tableId, message);

        _logger.LogInformation(
            "Chat message sent by {PlayerName} at table {TableId}",
            senderName, tableId);

        return Task.FromResult<(ChatMessageDto?, string?)>((message, null));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ChatMessageDto>> GetChatHistoryAsync(Guid tableId, int maxMessages = 50)
    {
        if (!_tableMessages.TryGetValue(tableId, out var messages))
        {
            return Task.FromResult<IReadOnlyList<ChatMessageDto>>(Array.Empty<ChatMessageDto>());
        }

        lock (messages)
        {
            var result = messages
                .OrderByDescending(m => m.Timestamp)
                .Take(Math.Min(maxMessages, _maxHistoryPerTable))
                .Reverse()
                .ToList();
            return Task.FromResult<IReadOnlyList<ChatMessageDto>>(result);
        }
    }

    /// <inheritdoc />
    public Task<ChatMessageDto> CreateSystemAnnouncementAsync(Guid tableId, string content)
    {
        var message = new ChatMessageDto(
            MessageId: Guid.NewGuid(),
            TableId: tableId,
            SenderName: "System",
            Content: content,
            MessageType: ChatMessageType.System,
            Timestamp: DateTime.UtcNow);

        AddMessageToHistory(tableId, message);

        _logger.LogDebug("System announcement at table {TableId}: {Content}", tableId, content);

        return Task.FromResult(message);
    }

    /// <inheritdoc />
    public Task<(bool Success, string? Error)> MutePlayerAsync(Guid tableId, string playerName, string playerToMute)
    {
        if (string.Equals(playerName, playerToMute, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<(bool, string?)>((false, "You cannot mute yourself."));
        }

        var tableMutes = _mutedPlayers.GetOrAdd(tableId, _ => new ConcurrentDictionary<string, HashSet<string>>());
        var playerMutes = tableMutes.GetOrAdd(playerName, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        lock (playerMutes)
        {
            if (playerMutes.Contains(playerToMute))
            {
                return Task.FromResult<(bool, string?)>((false, $"{playerToMute} is already muted."));
            }

            playerMutes.Add(playerToMute);
        }

        _logger.LogInformation(
            "Player {PlayerName} muted {MutedPlayer} at table {TableId}",
            playerName, playerToMute, tableId);

        return Task.FromResult<(bool, string?)>((true, null));
    }

    /// <inheritdoc />
    public Task<(bool Success, string? Error)> UnmutePlayerAsync(Guid tableId, string playerName, string playerToUnmute)
    {
        if (!_mutedPlayers.TryGetValue(tableId, out var tableMutes))
        {
            return Task.FromResult<(bool, string?)>((false, $"{playerToUnmute} is not muted."));
        }

        if (!tableMutes.TryGetValue(playerName, out var playerMutes))
        {
            return Task.FromResult<(bool, string?)>((false, $"{playerToUnmute} is not muted."));
        }

        lock (playerMutes)
        {
            if (!playerMutes.Remove(playerToUnmute))
            {
                return Task.FromResult<(bool, string?)>((false, $"{playerToUnmute} is not muted."));
            }
        }

        _logger.LogInformation(
            "Player {PlayerName} unmuted {UnmutedPlayer} at table {TableId}",
            playerName, playerToUnmute, tableId);

        return Task.FromResult<(bool, string?)>((true, null));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetMutedPlayersAsync(Guid tableId, string playerName)
    {
        if (!_mutedPlayers.TryGetValue(tableId, out var tableMutes))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        if (!tableMutes.TryGetValue(playerName, out var playerMutes))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        lock (playerMutes)
        {
            return Task.FromResult<IReadOnlyList<string>>(playerMutes.ToList());
        }
    }

    /// <inheritdoc />
    public Task<bool> IsPlayerMutedAsync(Guid tableId, string playerName, string mutedPlayerName)
    {
        if (!_mutedPlayers.TryGetValue(tableId, out var tableMutes))
        {
            return Task.FromResult(false);
        }

        if (!tableMutes.TryGetValue(playerName, out var playerMutes))
        {
            return Task.FromResult(false);
        }

        lock (playerMutes)
        {
            return Task.FromResult(playerMutes.Contains(mutedPlayerName));
        }
    }

    /// <inheritdoc />
    public Task<(bool Success, string? Error)> SetTableChatEnabledAsync(Guid tableId, bool enabled, string? changedByPlayerName = null)
    {
        _tableChatEnabled[tableId] = enabled;

        _logger.LogInformation(
            "Table {TableId} chat {Status} by {ChangedBy}",
            tableId, enabled ? "enabled" : "disabled", changedByPlayerName ?? "System");

        return Task.FromResult<(bool, string?)>((true, null));
    }

    /// <inheritdoc />
    public Task<bool> IsTableChatEnabledAsync(Guid tableId)
    {
        return Task.FromResult(_tableChatEnabled.GetOrAdd(tableId, true));
    }

    private void AddMessageToHistory(Guid tableId, ChatMessageDto message)
    {
        var messages = _tableMessages.GetOrAdd(tableId, _ => new List<ChatMessageDto>());

        lock (messages)
        {
            messages.Add(message);

            // Trim history if needed
            while (messages.Count > _maxHistoryPerTable)
            {
                messages.RemoveAt(0);
            }
        }
    }
}
