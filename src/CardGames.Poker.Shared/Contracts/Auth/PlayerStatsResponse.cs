namespace CardGames.Poker.Shared.Contracts.Auth;

public record PlayerStatsResponse(
    bool Success,
    int GamesPlayed = 0,
    int GamesWon = 0,
    int GamesLost = 0,
    long TotalChipsWon = 0,
    long TotalChipsLost = 0,
    string? Error = null
);
