namespace CardGames.Poker.Shared.Contracts.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string? DisplayName = null
);
