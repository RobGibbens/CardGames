using CardGames.Poker.Shared.DTOs;

namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Response for joining the waiting list.
/// </summary>
public record JoinWaitingListResponse(
    bool Success,
    WaitingListEntryDto? Entry = null,
    string? Error = null);
