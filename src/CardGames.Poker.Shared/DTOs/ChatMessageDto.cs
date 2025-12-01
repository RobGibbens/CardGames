using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.DTOs;

/// <summary>
/// DTO for a chat message at a table.
/// </summary>
public record ChatMessageDto(
    Guid MessageId,
    Guid TableId,
    string SenderName,
    string Content,
    ChatMessageType MessageType,
    DateTime Timestamp);
