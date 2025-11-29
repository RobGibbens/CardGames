namespace CardGames.Poker.Shared.Contracts.Auth;

public record ProfileResponse(
    bool Success,
    string? Id = null,
    string? Email = null,
    string? DisplayName = null,
    string? AvatarUrl = null,
    long? ChipBalance = null,
    string? Error = null
);
