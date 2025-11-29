namespace CardGames.Poker.Shared.Contracts.Auth;

public record UpdateChipBalanceRequest(
    long Amount,
    string? Reason = null
);

public record SetChipBalanceRequest(
    long Balance,
    string? Reason = null
);
