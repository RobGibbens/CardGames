namespace CardGames.Poker.Shared.Contracts.Auth;

public record UpdateProfileRequest(
    string? DisplayName = null,
    string? AvatarUrl = null
);
