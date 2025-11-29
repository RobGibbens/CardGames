namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Request to leave the waiting list for a table.
/// </summary>
public record LeaveWaitingListRequest(
    Guid TableId,
    string PlayerName);
