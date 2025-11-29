namespace CardGames.Poker.Shared.Contracts.Auth;

public record ChipBalanceResponse(
    bool Success,
    long? Balance = null,
    string? Error = null
);
