namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Request to select and reserve a specific seat at a table.
/// </summary>
public record SelectSeatRequest(
    Guid TableId,
    int SeatNumber,
    string PlayerName);
