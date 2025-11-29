namespace CardGames.Poker.Shared.DTOs;

public record FriendDto(
    string UserId,
    string? DisplayName,
    string? Email,
    string? AvatarUrl,
    DateTime FriendsSince
);
