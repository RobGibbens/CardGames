namespace CardGames.Poker.Shared.Contracts.Auth;

public record LoginRequest(
    string Email,
    string Password
);
