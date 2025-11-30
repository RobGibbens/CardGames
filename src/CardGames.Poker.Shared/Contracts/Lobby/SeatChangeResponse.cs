using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Response to a seat change request.
/// </summary>
public record SeatChangeResponse(
    bool Success,
    Guid? TableId = null,
    int? OldSeatNumber = null,
    int? NewSeatNumber = null,
    SeatDto? Seat = null,
    bool IsPending = false,
    string? Error = null);
