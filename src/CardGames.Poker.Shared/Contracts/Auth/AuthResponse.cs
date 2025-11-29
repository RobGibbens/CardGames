namespace CardGames.Poker.Shared.Contracts.Auth;

public record AuthResponse(
    bool Success,
    string? Token = null,
    string? Email = null,
    string? DisplayName = null,
    string? Error = null,
    DateTime? ExpiresAt = null
);
