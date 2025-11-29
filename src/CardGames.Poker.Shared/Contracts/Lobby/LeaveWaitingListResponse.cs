namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Response for leaving the waiting list.
/// </summary>
public record LeaveWaitingListResponse(
    bool Success,
    string? Error = null);
