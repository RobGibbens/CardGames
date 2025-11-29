using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Response for getting the waiting list for a table.
/// </summary>
public record GetWaitingListResponse(
    bool Success,
    IReadOnlyList<WaitingListEntryDto>? Entries = null,
    string? Error = null);
