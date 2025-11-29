using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Shared.Contracts.Lobby;

public record TablesListResponse(
    bool Success,
    IReadOnlyList<TableSummaryDto>? Tables = null,
    string? Error = null);
