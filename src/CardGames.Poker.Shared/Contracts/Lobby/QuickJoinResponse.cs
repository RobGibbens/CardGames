using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Shared.Contracts.Lobby;

public record QuickJoinResponse(
    bool Success,
    Guid? TableId = null,
    int? SeatNumber = null,
    TableSummaryDto? Table = null,
    string? Error = null);
