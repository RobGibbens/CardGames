namespace CardGames.Poker.Shared.Contracts.Lobby;

public record JoinTableResponse(
    bool Success,
    Guid? TableId = null,
    int? SeatNumber = null,
    string? Error = null);
