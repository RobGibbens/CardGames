namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Request to leave a table (freeing up a seat).
/// </summary>
public record LeaveTableRequest(
    Guid TableId,
    string PlayerName);
