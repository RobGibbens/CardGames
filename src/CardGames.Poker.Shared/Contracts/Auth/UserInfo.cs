namespace CardGames.Poker.Shared.Contracts.Auth;

public record UserInfo(
    string Id,
    string Email,
    string? DisplayName = null,
    bool IsAuthenticated = false,
    string? AuthProvider = null
);
