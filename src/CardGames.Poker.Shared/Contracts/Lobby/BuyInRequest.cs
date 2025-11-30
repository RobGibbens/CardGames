namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Request to complete buy-in and take a seat at a table.
/// </summary>
public record BuyInRequest(
    Guid TableId,
    int SeatNumber,
    string PlayerName,
    int BuyInAmount);
