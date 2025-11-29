using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Shared.Contracts.Lobby;

public record CreateTableResponse(
    bool Success,
    Guid? TableId = null,
    string? JoinLink = null,
    TableSummaryDto? Table = null,
    string? Error = null);
