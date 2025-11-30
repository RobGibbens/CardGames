namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Request to change seats at a table.
/// </summary>
public record SeatChangeRequest(
    Guid TableId,
    string PlayerName,
    int DesiredSeatNumber);
