namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Response for leaving a table.
/// </summary>
public record LeaveTableResponse(
    bool Success,
    string? Error = null);
