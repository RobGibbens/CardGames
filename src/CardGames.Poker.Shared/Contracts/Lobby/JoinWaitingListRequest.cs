namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Request to join the waiting list for a table.
/// </summary>
public record JoinWaitingListRequest(
    Guid TableId,
    string PlayerName);
