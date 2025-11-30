using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Response to a seat selection request.
/// </summary>
public record SelectSeatResponse(
    bool Success,
    Guid? TableId = null,
    int? SeatNumber = null,
    DateTime? ReservedUntil = null,
    SeatDto? Seat = null,
    string? Error = null);
