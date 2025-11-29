using CardGames.Poker.Shared.Enums;

namespace CardGames.Poker.Shared.DTOs;

public record FriendInvitationDto(
    string InvitationId,
    string SenderId,
    string? SenderDisplayName,
    string? SenderEmail,
    string? SenderAvatarUrl,
    string ReceiverId,
    string? ReceiverDisplayName,
    string? ReceiverEmail,
    string? ReceiverAvatarUrl,
    FriendshipStatus Status,
    DateTime CreatedAt
);
