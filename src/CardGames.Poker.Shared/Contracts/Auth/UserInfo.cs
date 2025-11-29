namespace CardGames.Poker.Shared.Contracts.Auth;

public record UserInfo(
    string Id,
    string Email,
    string? DisplayName = null,
    bool IsAuthenticated = false,
    string? AuthProvider = null,
    long ChipBalance = 0,
    string? AvatarUrl = null,
    int GamesPlayed = 0,
    int GamesWon = 0,
    int GamesLost = 0,
    long TotalChipsWon = 0,
    long TotalChipsLost = 0
);
