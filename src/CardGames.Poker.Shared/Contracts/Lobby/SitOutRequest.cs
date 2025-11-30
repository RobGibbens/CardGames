namespace CardGames.Poker.Shared.Contracts.Lobby;

/// <summary>
/// Request to toggle sit out status (sit out or sit back in).
/// </summary>
public record SitOutRequest(
    Guid TableId,
    string PlayerName,
    bool SitOut);
