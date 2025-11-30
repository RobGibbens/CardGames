using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Response to a buy-in request.
/// </summary>
public record BuyInResponse(
    bool Success,
    Guid? TableId = null,
    int? SeatNumber = null,
    int? ChipStack = null,
    SeatDto? Seat = null,
    string? Error = null);
