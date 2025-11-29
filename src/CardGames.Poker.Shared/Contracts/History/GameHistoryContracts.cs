namespace CardGames.Poker.Shared.Contracts.History;

public record GameHistoryDto(
    string Id,
    string GameType,
    DateTime StartedAt,
    DateTime EndedAt,
    bool Won,
    long ChipsWon,
    long ChipsLost,
    int PlayerCount,
    string? TableName = null,
    string? TournamentName = null,
    string? HandSummary = null
);

public record GameHistoryResponse(
    bool Success,
    IReadOnlyList<GameHistoryDto>? Items = null,
    int TotalCount = 0,
    int Page = 1,
    int PageSize = 20,
    int TotalPages = 0,
    string? Error = null
);
