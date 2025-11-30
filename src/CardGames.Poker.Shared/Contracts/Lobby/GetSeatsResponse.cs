using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Response containing seat information for a table.
/// </summary>
public record GetSeatsResponse(
    bool Success,
    Guid? TableId = null,
    IReadOnlyList<SeatDto>? Seats = null,
    string? Error = null);
